using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Application.Persistence;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;

namespace SongStash.Application.LibraryActions;

public sealed class ManualSongService(
    ISongRepository songs,
    IExternalReferenceRepository references,
    SongMatcher matcher,
    IManualEntryCommitter committer,
    IClock clock)
{
    public async Task<ManualAddResult> AddAsync(
        ManualSongInput input,
        Guid? attachToSongId = null,
        bool keepSeparate = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            return new ManualAddResult(ManualAddOutcome.Invalid, Message: "Title is required.");
        if (string.IsNullOrWhiteSpace(input.Artist))
            return new ManualAddResult(ManualAddOutcome.Invalid, Message: "Artist is required.");

        var library = await songs.GetAllForMatchingAsync(cancellationToken).ConfigureAwait(false);
        var externalReferences = await references.GetBySongIdsAsync(library.Select(song => song.Id).ToArray(), cancellationToken)
            .ConfigureAwait(false);
        var candidate = new ImportCandidate(input.Title.Trim(), input.Artist.Trim(), FindSource.Manual);
        var match = matcher.Match(candidate, library, externalReferences);

        if (attachToSongId.HasValue)
        {
            var validChoice = match.Status switch
            {
                SongMatchStatus.ExactMatch => match.MatchedSong?.Id == attachToSongId.Value,
                SongMatchStatus.NeedsReview => match.PossibleMatches.Any(song => song.Id == attachToSongId.Value),
                _ => false
            };
            if (!validChoice)
                return new ManualAddResult(ManualAddOutcome.NeedsReview, Match: match,
                    Message: "Choose one of the current matching songs.");

            var findEvent = CreateFindEvent(attachToSongId.Value, input.FoundAt);
            await committer.CommitFindEventAsync(findEvent, cancellationToken).ConfigureAwait(false);
            return new ManualAddResult(ManualAddOutcome.AddedFindToExistingSong, attachToSongId.Value, match);
        }

        if (!keepSeparate)
        {
            if (match.Status == SongMatchStatus.ExactMatch)
                return new ManualAddResult(ManualAddOutcome.ExactMatchRequiresChoice, Match: match);
            if (match.Status == SongMatchStatus.NeedsReview)
                return new ManualAddResult(ManualAddOutcome.NeedsReview, Match: match);
        }

        var song = new Song(input.Title.Trim(), input.Artist.Trim(), Clean(input.Album), input.ArtworkUrl,
            Clean(input.Genre), Clean(input.Notes), clock.UtcNow);
        await committer.CommitNewSongAsync(song, CreateFindEvent(song.Id, input.FoundAt), cancellationToken)
            .ConfigureAwait(false);
        return new ManualAddResult(ManualAddOutcome.AddedNewSong, song.Id, match);
    }

    private FindEvent CreateFindEvent(Guid songId, DateTimeOffset? foundAt) =>
        new(songId, FindSource.Manual, clock.UtcNow, foundAt);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
