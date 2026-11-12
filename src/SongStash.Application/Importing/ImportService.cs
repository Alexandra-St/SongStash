using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;

namespace SongStash.Application.Importing;

public sealed class ImportService
{
    private readonly ISongRepository _songs;
    private readonly IFindEventRepository _findEvents;
    private readonly IImportItemCommitter _committer;
    private readonly FindEventDeduplicator _deduplicator;
    private readonly IClock _clock;

    public ImportService(
        ISongRepository songs,
        IFindEventRepository findEvents,
        IImportItemCommitter committer,
        FindEventDeduplicator deduplicator,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(songs);
        ArgumentNullException.ThrowIfNull(findEvents);
        ArgumentNullException.ThrowIfNull(committer);
        ArgumentNullException.ThrowIfNull(deduplicator);
        ArgumentNullException.ThrowIfNull(clock);

        _songs = songs;
        _findEvents = findEvents;
        _committer = committer;
        _deduplicator = deduplicator;
        _clock = clock;
    }

    public async Task<ImportResult> ImportAsync(
        IEnumerable<ImportPreviewItem> previewItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewItems);

        var results = new List<ImportItemResult>();
        var songsAddedInThisRun = new Dictionary<ReferenceKey, Guid>();
        foreach (var item in previewItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ImportItemAsync(item, songsAddedInThisRun, cancellationToken).ConfigureAwait(false));
        }

        return new ImportResult(results);
    }

    private async Task<ImportItemResult> ImportItemAsync(
        ImportPreviewItem item,
        IDictionary<ReferenceKey, Guid> songsAddedInThisRun,
        CancellationToken cancellationToken)
    {
        switch (item.Status)
        {
            case ImportPreviewStatus.New:
                var sameRunMatch = FindSameRunMatch(item.Candidate, songsAddedInThisRun);
                if (sameRunMatch is { } existingId)
                {
                    var matchedItem = item with { Status = ImportPreviewStatus.AlreadyInLibrary, MatchedSongId = existingId };
                    return await ImportEventForExistingSongAsync(matchedItem, cancellationToken).ConfigureAwait(false);
                }
                return await ImportNewSongAsync(item, songsAddedInThisRun, cancellationToken).ConfigureAwait(false);
            case ImportPreviewStatus.AlreadyInLibrary:
                return await ImportEventForExistingSongAsync(item, cancellationToken).ConfigureAwait(false);
            case ImportPreviewStatus.Incomplete:
                return NeedsAttention(item, "Complete the title and artist before importing this item.");
            case ImportPreviewStatus.NeedsReview:
                return NeedsAttention(item, "Review the possible song matches before importing this item.");
            default:
                return NeedsAttention(item, "This preview item cannot be imported automatically.");
        }
    }

    private async Task<ImportItemResult> ImportNewSongAsync(
        ImportPreviewItem item,
        IDictionary<ReferenceKey, Guid> songsAddedInThisRun,
        CancellationToken cancellationToken)
    {
        var candidate = item.Candidate;
        if (string.IsNullOrWhiteSpace(candidate.Title) || string.IsNullOrWhiteSpace(candidate.Artist))
        {
            return NeedsAttention(item, "Complete the title and artist before importing this item.");
        }

        var now = _clock.UtcNow.ToUniversalTime();
        var song = new Song(candidate.Title, candidate.Artist, candidate.Album, candidate.ArtworkUrl,
            genre: null, notes: null, createdAt: now);
        var references = CreateDistinctReferences(song.Id, candidate.ExternalReferences);
        var findEvent = CreateFindEvent(song.Id, candidate, now);

        try
        {
            await _committer.CommitNewSongAsync(song, references, findEvent, cancellationToken)
                .ConfigureAwait(false);
            RememberReferences(song.Id, candidate.ExternalReferences, songsAddedInThisRun);
            return new ImportItemResult(item, ImportItemOutcomeStatus.AddedSong);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Failed(item);
        }
    }

    private async Task<ImportItemResult> ImportEventForExistingSongAsync(
        ImportPreviewItem item,
        CancellationToken cancellationToken)
    {
        if (item.MatchedSongId is not { } songId)
        {
            return NeedsAttention(item, "The matched song is no longer available. Refresh the preview.");
        }

        Song? song;
        try
        {
            song = await _songs.GetByIdAsync(songId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Failed(item);
        }

        if (song is null)
        {
            return NeedsAttention(item, "The matched song is no longer available. Refresh the preview.");
        }

        IReadOnlyList<FindEvent> existingEvents;
        try
        {
            existingEvents = await _findEvents.GetBySongIdAsync(song.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Failed(item);
        }

        var deduplication = _deduplicator.Evaluate(item.Candidate, existingEvents);
        switch (deduplication.Status)
        {
            case FindEventDeduplicationStatus.Duplicate:
                return new ImportItemResult(item, ImportItemOutcomeStatus.SkippedDuplicate);
            case FindEventDeduplicationStatus.Indeterminate:
                return NeedsAttention(item, "There is not enough event identity data to import safely.");
            case FindEventDeduplicationStatus.New:
                var findEvent = CreateFindEvent(song.Id, item.Candidate, _clock.UtcNow.ToUniversalTime());
                try
                {
                    await _committer.CommitFindEventAsync(findEvent, cancellationToken).ConfigureAwait(false);
                    return new ImportItemResult(item, ImportItemOutcomeStatus.AddedFindEvent);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    return Failed(item);
                }
            default:
                return NeedsAttention(item, "The event match could not be determined safely.");
        }
    }

    private static FindEvent CreateFindEvent(Guid songId, ImportCandidate candidate, DateTimeOffset importedAt) =>
        new(songId, candidate.Source, importedAt, candidate.FoundAt, candidate.SourceItemId);

    private static Guid? FindSameRunMatch(ImportCandidate candidate, IDictionary<ReferenceKey, Guid> songsAddedInThisRun)
    {
        foreach (var reference in candidate.ExternalReferences)
        {
            var value = reference.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value) && songsAddedInThisRun.TryGetValue(
                    new ReferenceKey(reference.Provider, value, null), out var songId))
                return songId;
        }
        return null;
    }

    private static void RememberReferences(
        Guid songId,
        IEnumerable<ExternalReferenceCandidate> candidates,
        IDictionary<ReferenceKey, Guid> songsAddedInThisRun)
    {
        foreach (var candidate in candidates)
        {
            var value = candidate.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                songsAddedInThisRun.TryAdd(new ReferenceKey(candidate.Provider, value, null), songId);
        }
    }

    private static IReadOnlyList<ExternalReference> CreateDistinctReferences(
        Guid songId,
        IEnumerable<ExternalReferenceCandidate> candidates)
    {
        var references = new List<ExternalReference>();
        var seen = new HashSet<ReferenceKey>();
        foreach (var candidate in candidates)
        {
            var externalId = candidate.Value?.Trim();
            Uri? url = candidate.Url;
            if (url is not null && externalId is not null &&
                Uri.TryCreate(externalId, UriKind.Absolute, out var valueUri) &&
                Uri.Compare(valueUri, url, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                externalId = null;
            }

            if (string.IsNullOrWhiteSpace(externalId))
            {
                externalId = null;
            }

            if (externalId is null && url is null)
            {
                continue;
            }

            var key = new ReferenceKey(candidate.Provider, externalId, url);
            if (seen.Add(key))
            {
                references.Add(new ExternalReference(songId, candidate.Provider, externalId, url));
            }
        }

        return references;
    }

    private static ImportItemResult NeedsAttention(ImportPreviewItem item, string message) =>
        new(item, ImportItemOutcomeStatus.NeedsAttention, message);

    private static ImportItemResult Failed(ImportPreviewItem item) =>
        new(item, ImportItemOutcomeStatus.Failed, "Could not save this item. You can try importing it again.");

    private sealed record ReferenceKey(ExternalReferenceProvider Provider, string? ExternalId, Uri? Url);
}
