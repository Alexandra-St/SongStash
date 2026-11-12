using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SongStash.Application.Exporting;
using SongStash.Application.Importing;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;

namespace SongStash.Infrastructure.Exporting;

public sealed class LibraryExportService : ILibraryExportService
{
    private const int SchemaVersion = 1;
    private const string FormatIdentifier = "SongStash.LibraryExport";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ISongRepository _songs;
    private readonly IFindEventRepository _findEvents;
    private readonly IExternalReferenceRepository _externalReferences;
    private readonly IClock _clock;

    public LibraryExportService(
        ISongRepository songs,
        IFindEventRepository findEvents,
        IExternalReferenceRepository externalReferences,
        IClock clock)
    {
        _songs = songs;
        _findEvents = findEvents;
        _externalReferences = externalReferences;
        _clock = clock;
    }

    public async Task ExportJsonAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var songs = await ReadLibraryAsync(cancellationToken).ConfigureAwait(false);
        var document = new LibraryExportDocument(
            FormatIdentifier,
            SchemaVersion,
            _clock.UtcNow.ToUniversalTime(),
            songs);
        await JsonSerializer.SerializeAsync(destination, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportCsvAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var songs = await ReadLibraryAsync(cancellationToken).ConfigureAwait(false);
        await using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\r\n"
        };

        await writer.WriteLineAsync(CsvHeader.AsMemory(), cancellationToken).ConfigureAwait(false);
        foreach (var song in songs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (song.FindEvents.Count == 0)
            {
                await WriteCsvRowAsync(writer, song, null, cancellationToken).ConfigureAwait(false);
                continue;
            }

            foreach (var findEvent in song.FindEvents)
            {
                await WriteCsvRowAsync(writer, song, findEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SongExportData>> ReadLibraryAsync(CancellationToken cancellationToken)
    {
        var songs = (await _songs.GetAllForMatchingAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(song => song.Id)
            .ToArray();
        var songIds = songs.Select(song => song.Id).ToArray();
        IReadOnlyList<ExternalReference> references = songIds.Length == 0
            ? Array.Empty<ExternalReference>()
            : await _externalReferences.GetBySongIdsAsync(songIds, cancellationToken).ConfigureAwait(false);
        var referencesBySong = references
            .GroupBy(reference => reference.SongId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ExternalReference>)group
                .OrderBy(reference => reference.Provider)
                .ThenBy(reference => reference.ExternalId, StringComparer.Ordinal)
                .ThenBy(reference => reference.Url?.ToString(), StringComparer.Ordinal)
                .ThenBy(reference => reference.Id)
                .ToArray());

        var exported = new List<SongExportData>(songs.Length);
        foreach (var song in songs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var findEvents = await _findEvents.GetBySongIdAsync(song.Id, cancellationToken).ConfigureAwait(false);
            var orderedEvents = findEvents
                .OrderBy(find => find.FoundAt?.UtcDateTime.Ticks ?? long.MinValue)
                .ThenBy(find => find.ImportedAt.UtcDateTime.Ticks)
                .ThenBy(find => find.Id)
                .ToArray();
            referencesBySong.TryGetValue(song.Id, out var songReferences);
            exported.Add(new SongExportData(
                song.Id,
                song.Title,
                song.Artist,
                song.Album,
                song.ArtworkUrl?.ToString(),
                song.Genre,
                song.Notes,
                song.IsFavorite,
                song.CreatedAt,
                song.UpdatedAt,
                orderedEvents.Select(find => new FindEventExportDto(
                    find.Id,
                    find.SongId,
                    find.FoundAt,
                    find.Source,
                    find.ImportedAt,
                    find.SourceItemId)).ToArray(),
                (songReferences ?? []).Select(reference => new ExternalReferenceExportDto(
                    reference.Id,
                    reference.SongId,
                    reference.Provider,
                    reference.ExternalId,
                    reference.Url?.ToString())).ToArray()));
        }

        return exported;
    }

    private static async Task WriteCsvRowAsync(
        StreamWriter writer,
        SongExportData song,
        FindEventExportDto? findEvent,
        CancellationToken cancellationToken)
    {
        var references = song.ExternalReferences;
        var columns = new string?[]
        {
            song.Id.ToString(),
            song.Title,
            song.Artist,
            song.Album,
            song.ArtworkUrl,
            song.Genre,
            song.Notes,
            song.IsFavorite ? "true" : "false",
            FormatTimestamp(findEvent?.FoundAt),
            findEvent?.Source.ToString(),
            FormatTimestamp(findEvent?.ImportedAt),
            findEvent?.SourceItemId,
            GetIds(references, ExternalReferenceProvider.Shazam),
            GetUrls(references, ExternalReferenceProvider.Shazam),
            GetIds(references, ExternalReferenceProvider.AppleMusic),
            GetIds(references, ExternalReferenceProvider.Isrc)
        };

        await writer.WriteLineAsync(string.Join(',', columns.Select(EscapeCsv)).AsMemory(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? GetIds(IReadOnlyList<ExternalReferenceExportDto> references, ExternalReferenceProvider provider) =>
        SerializeMultiple(references.Where(reference => reference.Provider == provider)
            .Select(reference => reference.ExternalId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

    private static string? GetUrls(IReadOnlyList<ExternalReferenceExportDto> references, ExternalReferenceProvider provider) =>
        SerializeMultiple(references.Where(reference => reference.Provider == provider)
            .Select(reference => reference.Url)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

    private static string? SerializeMultiple(IEnumerable<string> values)
    {
        var items = values.ToArray();
        return items.Length switch
        {
            0 => null,
            1 => items[0],
            _ => JsonSerializer.Serialize(items)
        };
    }

    private static string? FormatTimestamp(DateTimeOffset? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static string CsvHeader =>
        "SongId,Title,Artist,Album,ArtworkUrl,Genre,Notes,IsFavorite,FoundAt,Source,ImportedAt,SourceItemId,ShazamId,ShazamUrl,AppleMusicId,Isrc";

    private sealed record LibraryExportDocument(
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("exportedAt")] DateTimeOffset ExportedAt,
        [property: JsonPropertyName("songs")] IReadOnlyList<SongExportData> Songs);

    private sealed record SongExportData(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("artist")] string Artist,
        [property: JsonPropertyName("album")] string? Album,
        [property: JsonPropertyName("artworkUrl")] string? ArtworkUrl,
        [property: JsonPropertyName("genre")] string? Genre,
        [property: JsonPropertyName("notes")] string? Notes,
        [property: JsonPropertyName("isFavorite")] bool IsFavorite,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
        [property: JsonPropertyName("findEvents")] IReadOnlyList<FindEventExportDto> FindEvents,
        [property: JsonPropertyName("externalReferences")] IReadOnlyList<ExternalReferenceExportDto> ExternalReferences);

    private sealed record FindEventExportDto(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("songId")] Guid SongId,
        [property: JsonPropertyName("foundAt")] DateTimeOffset? FoundAt,
        [property: JsonPropertyName("source")] FindSource Source,
        [property: JsonPropertyName("importedAt")] DateTimeOffset ImportedAt,
        [property: JsonPropertyName("sourceItemId")] string? SourceItemId);

    private sealed record ExternalReferenceExportDto(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("songId")] Guid SongId,
        [property: JsonPropertyName("provider")] ExternalReferenceProvider Provider,
        [property: JsonPropertyName("externalId")] string? ExternalId,
        [property: JsonPropertyName("url")] string? Url);
}
