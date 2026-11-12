using System.Text;
using System.Text.Json;
using SongStash.Application.Importing;

namespace SongStash.Infrastructure.Importing;

/// <summary>Reads a flat JSON array or an object containing a flat songs array.</summary>
public sealed class JsonImportSourceReader(string path) : IImportSourceReader
{
    private readonly string _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));

    public async Task<ImportReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var items = GetItems(document.RootElement);
            if (items is null)
                return ImportReadResult.UnsupportedSource(new ImportDiagnostic(ImportDiagnosticSeverity.Error,
                    "json.shape.unsupported", "JSON must be an array of records or an object with a songs array."));

            var candidates = new List<ImportCandidate>();
            var sourceDiagnostics = new List<ImportDiagnostic>();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.ValueKind != JsonValueKind.Object)
                {
                    sourceDiagnostics.Add(new ImportDiagnostic(ImportDiagnosticSeverity.Warning,
                        "json.row.invalid", "A non-object record was skipped."));
                    continue;
                }
                var diagnostics = new List<ImportDiagnostic>();
                candidates.Add(ImportReaderMapping.CreateCandidate(
                    ImportReaderMapping.GetProperty(item, "title", diagnostics),
                    ImportReaderMapping.GetProperty(item, "artist", diagnostics),
                    ImportReaderMapping.GetProperty(item, "album", diagnostics),
                    ImportReaderMapping.GetProperty(item, "artworkUrl", diagnostics),
                    ImportReaderMapping.GetProperty(item, "foundAt", diagnostics),
                    ImportReaderMapping.GetProperty(item, "source", diagnostics),
                    ImportReaderMapping.GetProperty(item, "sourceItemId", diagnostics),
                    ImportReaderMapping.GetProperty(item, "shazamId", diagnostics),
                    ImportReaderMapping.GetProperty(item, "shazamUrl", diagnostics),
                    ImportReaderMapping.GetProperty(item, "appleMusicId", diagnostics),
                    ImportReaderMapping.GetProperty(item, "isrc", diagnostics), diagnostics));
            }
            return ImportReadResult.Success(candidates, sourceDiagnostics);
        }
        catch (OperationCanceledException) { throw; }
        catch (FileNotFoundException) { return Unavailable("file.not_found", "The selected file could not be found."); }
        catch (DirectoryNotFoundException) { return Unavailable("file.not_found", "The selected file could not be found."); }
        catch (UnauthorizedAccessException) { return Unavailable("file.access_denied", "SongStash does not have access to the selected file."); }
        catch (IOException) { return Unavailable("file.read_failed", "The selected file could not be read."); }
        catch (JsonException) { return ImportReadResult.UnsupportedSource(new ImportDiagnostic(
            ImportDiagnosticSeverity.Error, "json.malformed", "The JSON file is malformed or is not valid UTF-8.")); }
    }

    private static IReadOnlyList<JsonElement>? GetItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray().ToArray();
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, "songs", StringComparison.OrdinalIgnoreCase)) continue;
            return property.Value.ValueKind == JsonValueKind.Array ? property.Value.EnumerateArray().ToArray() : null;
        }
        return null;
    }

    private static ImportReadResult Unavailable(string code, string message) => ImportReadResult.Unavailable(
        new ImportDiagnostic(ImportDiagnosticSeverity.Error, code, message));
}
