namespace SongStash.Application.Library;

public sealed record LibrarySongItem(
    Guid Id,
    string Title,
    string Artist,
    string? Album,
    Uri? ArtworkUrl,
    bool IsFavorite,
    int FindCount,
    DateTimeOffset? LatestFoundAt = null,
    DateTimeOffset? OldestFoundAt = null);
