using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;

namespace SongStash.Application.Persistence;

/// <summary>Atomically persists the writes belonging to one import preview item.</summary>
public interface IImportItemCommitter
{
    Task CommitNewSongAsync(
        Song song,
        IReadOnlyCollection<ExternalReference> externalReferences,
        FindEvent findEvent,
        CancellationToken cancellationToken = default);

    Task CommitFindEventAsync(FindEvent findEvent, CancellationToken cancellationToken = default);
}
