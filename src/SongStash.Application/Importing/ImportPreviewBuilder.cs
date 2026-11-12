using SongStash.Application.Matching;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Songs;

namespace SongStash.Application.Importing;

/// <summary>Builds an in-memory preview without persisting or deduplicating candidates.</summary>
public sealed class ImportPreviewBuilder
{
    private readonly SongMatcher _songMatcher;

    public ImportPreviewBuilder(SongMatcher songMatcher)
    {
        ArgumentNullException.ThrowIfNull(songMatcher);
        _songMatcher = songMatcher;
    }

    public IReadOnlyList<ImportPreviewItem> Build(
        IEnumerable<ImportCandidate> candidates,
        IEnumerable<Song> songs,
        IEnumerable<ExternalReference> externalReferences)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(songs);
        ArgumentNullException.ThrowIfNull(externalReferences);

        var existingSongs = songs.ToArray();
        var existingReferences = externalReferences.ToArray();

        return candidates.Select(candidate => BuildItem(candidate, existingSongs, existingReferences)).ToArray();
    }

    private ImportPreviewItem BuildItem(
        ImportCandidate candidate,
        IReadOnlyList<Song> songs,
        IReadOnlyList<ExternalReference> externalReferences)
    {
        if (string.IsNullOrWhiteSpace(candidate.Title) || string.IsNullOrWhiteSpace(candidate.Artist))
        {
            return new ImportPreviewItem(
                candidate,
                ImportPreviewStatus.Incomplete,
                null,
                [],
                null,
                candidate.Diagnostics);
        }

        var match = _songMatcher.Match(candidate, songs, externalReferences);
        return match.Status switch
        {
            SongMatchStatus.ExactMatch when match.MatchedSong is not null => new ImportPreviewItem(
                candidate,
                ImportPreviewStatus.AlreadyInLibrary,
                match.MatchedSong.Id,
                [],
                match.Reason,
                candidate.Diagnostics),
            SongMatchStatus.NeedsReview => new ImportPreviewItem(
                candidate,
                ImportPreviewStatus.NeedsReview,
                null,
                match.PossibleMatches.Select(song => song.Id).ToArray(),
                match.Reason,
                candidate.Diagnostics),
            _ => new ImportPreviewItem(
                candidate,
                ImportPreviewStatus.New,
                null,
                [],
                null,
                candidate.Diagnostics)
        };
    }
}
