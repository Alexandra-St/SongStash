using SongStash.Domain.Songs;

namespace SongStash.Application.Matching;

public sealed record SongMatchResult(
    SongMatchStatus Status,
    SongMatchReason Reason,
    Song? MatchedSong,
    IReadOnlyList<Song> PossibleMatches)
{
    public static SongMatchResult NoMatch() => new(SongMatchStatus.NoMatch, SongMatchReason.None, null, []);

    public static SongMatchResult Exact(Song song, SongMatchReason reason) =>
        new(SongMatchStatus.ExactMatch, reason, song, [song]);

    public static SongMatchResult NeedsReview(IEnumerable<Song> possibleMatches) =>
        new(SongMatchStatus.NeedsReview, SongMatchReason.Ambiguous, null, possibleMatches.ToArray());
}
