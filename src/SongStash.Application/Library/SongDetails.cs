using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;

namespace SongStash.Application.Library;

public sealed record SongDetails(
    Guid Id,
    string Title,
    string Artist,
    string? Album,
    Uri? ArtworkUrl,
    string? Genre,
    string? Notes,
    bool IsFavorite,
    IReadOnlyList<FindEvent> FindEvents,
    IReadOnlyList<ExternalReference> ExternalReferences);
