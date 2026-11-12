using SongStash.Domain.Finds;
using SongStash.Domain.Songs;

namespace SongStash.Application.Persistence;

/// <summary>Atomically commits the writes for one manual entry.</summary>
public interface IManualEntryCommitter
{
    Task CommitNewSongAsync(Song song, FindEvent findEvent, CancellationToken cancellationToken = default);
    Task CommitFindEventAsync(FindEvent findEvent, CancellationToken cancellationToken = default);
}
