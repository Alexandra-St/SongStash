using System.Text;
using SongStash.Application.Importing;

namespace SongStash.Infrastructure.Importing;

/// <summary>Reads the documented flat SongStash metadata CSV fields without importing SongStash-only metadata.</summary>
public sealed class CsvImportSourceReader(string path) : IImportSourceReader
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "title", ["song"] = "title", ["track"] = "title",
        ["artist"] = "artist", ["performer"] = "artist",
        ["album"] = "album", ["artworkurl"] = "artworkurl", ["foundat"] = "foundat",
        ["datefound"] = "foundat", ["founddate"] = "foundat", ["source"] = "source",
        ["sourceitemid"] = "sourceitemid", ["shazamid"] = "shazamid", ["shazamurl"] = "shazamurl",
        ["applemusicid"] = "applemusicid", ["isrc"] = "isrc"
    };

    private static readonly string[] RequiredHeaders = ["title", "artist"];
    private readonly string _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));

    public async Task<ImportReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        string text;
        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (FileNotFoundException) { return Unavailable("file.not_found", "The selected file could not be found."); }
        catch (DirectoryNotFoundException) { return Unavailable("file.not_found", "The selected file could not be found."); }
        catch (UnauthorizedAccessException) { return Unavailable("file.access_denied", "SongStash does not have access to the selected file."); }
        catch (IOException) { return Unavailable("file.read_failed", "The selected file could not be read."); }
        catch (DecoderFallbackException) { return Unsupported("csv.encoding.invalid", "The CSV file is not valid UTF-8."); }

        if (string.IsNullOrWhiteSpace(text)) return ImportReadResult.UnsupportedSource(
            new ImportDiagnostic(ImportDiagnosticSeverity.Error, "csv.empty", "The CSV file is empty."));

        List<List<string>> rows;
        try { rows = ParseCsv(text, cancellationToken); }
        catch (FormatException) { return ImportReadResult.UnsupportedSource(
            new ImportDiagnostic(ImportDiagnosticSeverity.Error, "csv.malformed", "The CSV file has invalid quoting or row structure.")); }

        var headerRowIndex = rows.FindIndex(row => row.Any(field => !string.IsNullOrWhiteSpace(field)));
        if (headerRowIndex < 0) return ImportReadResult.UnsupportedSource(
            new ImportDiagnostic(ImportDiagnosticSeverity.Error, "csv.headers.missing", "The CSV file does not contain a header row."));
        var headers = rows[headerRowIndex].Select(header => Aliases.TryGetValue(header.Trim(), out var alias) ? alias : string.Empty).ToArray();
        var missing = RequiredHeaders.Where(required => !headers.Contains(required, StringComparer.Ordinal)).ToArray();
        if (missing.Length > 0)
            return ImportReadResult.UnsupportedSource(new ImportDiagnostic(ImportDiagnosticSeverity.Error,
                "csv.columns.missing", "The CSV file must include Title and Artist columns."));

        var candidates = new List<ImportCandidate>();
        for (var rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var column = 0; column < headers.Length; column++)
            {
                if (headers[column].Length == 0) continue;
                var value = column < row.Count ? row[column] : string.Empty;
                if (!values.ContainsKey(headers[column])) values[headers[column]] = value;
            }
            var diagnostics = new List<ImportDiagnostic>();
            candidates.Add(ImportReaderMapping.CreateCandidate(
                Get(values, "title"), Get(values, "artist"), Get(values, "album"), Get(values, "artworkurl"),
                Get(values, "foundat"), Get(values, "source"), Get(values, "sourceitemid"),
                Get(values, "shazamid"), Get(values, "shazamurl"), Get(values, "applemusicid"), Get(values, "isrc"), diagnostics));
        }
        return ImportReadResult.Success(candidates);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> values, string key) => values.GetValueOrDefault(key);

    private static List<List<string>> ParseCsv(string text, CancellationToken cancellationToken)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var afterQuote = false;
        var fieldStart = true;

        for (var i = 0; i < text.Length; i++)
        {
            if ((i & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { inQuotes = false; afterQuote = true; }
                }
                else field.Append(ch);
                continue;
            }

            if (afterQuote)
            {
                if (ch == ',') { FinishField(); continue; }
                if (ch is '\r' or '\n') { FinishRow(); if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; continue; }
                throw new FormatException();
            }

            if (fieldStart && ch == '"') { inQuotes = true; fieldStart = false; continue; }
            if (ch == '"') throw new FormatException();
            if (ch == ',') { FinishField(); continue; }
            if (ch is '\r' or '\n') { FinishRow(); if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; continue; }
            field.Append(ch);
            fieldStart = false;
        }

        if (inQuotes) throw new FormatException();
        if (afterQuote || field.Length > 0 || row.Count > 0) FinishRow();
        return rows;

        void FinishField()
        {
            row.Add(field.ToString()); field.Clear(); fieldStart = true; afterQuote = false;
        }
        void FinishRow()
        {
            FinishField(); rows.Add(row); row = []; fieldStart = true;
        }
    }

    private static ImportReadResult Unavailable(string code, string message) => ImportReadResult.Unavailable(
        new ImportDiagnostic(ImportDiagnosticSeverity.Error, code, message));
    private static ImportReadResult Unsupported(string code, string message) => ImportReadResult.UnsupportedSource(
        new ImportDiagnostic(ImportDiagnosticSeverity.Error, code, message));
}
