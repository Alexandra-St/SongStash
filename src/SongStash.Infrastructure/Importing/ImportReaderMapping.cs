using System.Globalization;
using System.Text.Json;
using SongStash.Application.Importing;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;

namespace SongStash.Infrastructure.Importing;

internal static class ImportReaderMapping
{
    public static readonly ImportDiagnostic EmptyFile = Diagnostic("file.empty", "The selected file is empty.");

    public static ImportDiagnostic Diagnostic(string code, string message) =>
        new(ImportDiagnosticSeverity.Warning, code, message);

    public static bool TryParseSource(string? value, out FindSource source)
    {
        source = FindSource.ImportedUnknown;
        return value is not null &&
               Enum.TryParse(value.Trim(), ignoreCase: true, out source) &&
               Enum.IsDefined(source) &&
               !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    public static DateTimeOffset? ParseFoundAt(string? value, ICollection<ImportDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
        {
            diagnostics.Add(Diagnostic("date.precision.unsupported",
                "A date without a time and timezone was not imported as a find event."));
            return null;
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) && HasExplicitOffset(trimmed))
        {
            return parsed;
        }

        diagnostics.Add(Diagnostic("date.invalid", "The found date was not a valid ISO-8601 timestamp with a timezone."));
        return null;
    }

    public static ImportCandidate CreateCandidate(
        string? title,
        string? artist,
        string? album,
        string? artworkUrl,
        string? foundAt,
        string? sourceValue,
        string? sourceItemId,
        string? shazamId,
        string? shazamUrl,
        string? appleMusicId,
        string? isrc,
        ICollection<ImportDiagnostic> diagnostics)
    {
        title = title?.Trim() ?? string.Empty;
        artist = artist?.Trim() ?? string.Empty;
        if (title.Length == 0 || artist.Length == 0)
            diagnostics.Add(Diagnostic("row.incomplete", "The row is missing a title or artist."));

        var source = FindSource.ImportedUnknown;
        if (!string.IsNullOrWhiteSpace(sourceValue) && !TryParseSource(sourceValue, out source))
        {
            source = FindSource.ImportedUnknown;
            diagnostics.Add(Diagnostic("source.invalid", "The source value was not recognized; Imported Unknown was used."));
        }

        var artwork = ParseAbsoluteUri(artworkUrl, "artwork.invalid", diagnostics);
        var found = ParseFoundAt(foundAt, diagnostics);
        var references = new List<ExternalReferenceCandidate>();
        AddShazamReferences(shazamId, shazamUrl, references, diagnostics);
        AddIdentifiers(appleMusicId, ExternalReferenceProvider.AppleMusic, references, diagnostics);
        AddIdentifiers(isrc, ExternalReferenceProvider.Isrc, references, diagnostics);

        return new ImportCandidate(title, artist, source)
        {
            Album = NullIfEmpty(album),
            ArtworkUrl = artwork,
            FoundAt = found,
            SourceItemId = NullIfEmpty(sourceItemId),
            ExternalReferences = references,
            Diagnostics = diagnostics.ToArray()
        };
    }

    public static string? ReadJsonScalar(JsonElement element, ICollection<ImportDiagnostic> diagnostics, string field)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString();
        if (element.ValueKind == JsonValueKind.Number) return element.GetRawText();
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        diagnostics.Add(Diagnostic("row.value.invalid", $"The {field} value was not a supported scalar."));
        return null;
    }

    public static string? GetProperty(JsonElement item, string name, ICollection<ImportDiagnostic> diagnostics)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return ReadJsonScalar(property.Value, diagnostics, name);
        }
        return null;
    }

    private static bool HasExplicitOffset(string value) =>
        value.EndsWith('Z') || value.EndsWith('z') ||
        (value.Length >= 6 && (value[^6] == '+' || value[^6] == '-') && value[^3] == ':') ||
        (value.Length >= 5 && (value[^5] == '+' || value[^5] == '-') && char.IsDigit(value[^4]));

    private static Uri? ParseAbsoluteUri(string? value, string code, ICollection<ImportDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return uri;
        diagnostics.Add(Diagnostic(code, "A URL value was invalid and was skipped."));
        return null;
    }

    private static void AddIdentifiers(
        string? value,
        ExternalReferenceProvider provider,
        ICollection<ExternalReferenceCandidate> references,
        ICollection<ImportDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var values = ParseMultiple(value);
        foreach (var item in values)
        {
            if (!string.IsNullOrWhiteSpace(item)) references.Add(new ExternalReferenceCandidate(provider, item));
            else diagnostics.Add(Diagnostic("reference.invalid", "An empty external reference was skipped."));
        }
    }

    private static void AddShazamReferences(
        string? idValue,
        string? urlValue,
        ICollection<ExternalReferenceCandidate> references,
        ICollection<ImportDiagnostic> diagnostics)
    {
        var ids = string.IsNullOrWhiteSpace(idValue) ? [] : ParseMultiple(idValue);
        var urlTexts = string.IsNullOrWhiteSpace(urlValue) ? [] : ParseMultiple(urlValue);
        var urls = new List<Uri?>();
        foreach (var text in urlTexts)
        {
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
                urls.Add(uri);
            else
            {
                urls.Add(null);
                diagnostics.Add(Diagnostic("reference.url.invalid", "A Shazam URL was invalid and was skipped."));
            }
        }

        if (ids.Count > 0 && ids.Count == urls.Count)
        {
            for (var i = 0; i < ids.Count; i++)
                references.Add(new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, ids[i], urls[i]));
            return;
        }

        foreach (var id in ids)
            references.Add(new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, id));
        foreach (var uri in urls.Where(uri => uri is not null))
            references.Add(new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, uri!.AbsoluteUri, uri));
    }

    private static IReadOnlyList<string> ParseMultiple(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                    return document.RootElement.EnumerateArray()
                        .Where(element => element.ValueKind == JsonValueKind.String)
                        .Select(element => element.GetString() ?? string.Empty).ToArray();
            }
            catch (JsonException) { }
        }
        return [trimmed];
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
