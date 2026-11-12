namespace SongStash.Application.LibraryActions;

public sealed record ManualSongInput(
    string Title,
    string Artist,
    string? Album = null,
    string? Genre = null,
    string? Notes = null,
    DateTimeOffset? FoundAt = null,
    Uri? ArtworkUrl = null);
