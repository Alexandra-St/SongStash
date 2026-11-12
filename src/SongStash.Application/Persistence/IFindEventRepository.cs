using SongStash.Domain.Finds;

namespace SongStash.Application.Persistence;

public interface IFindEventRepository
{
    Task<IReadOnlyList<FindEvent>> GetForSongsAsync(
        IReadOnlyCollection<Guid> songIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FindEvent>> GetBySongIdAsync(Guid songId, CancellationToken cancellationToken = default);
    Task AddAsync(FindEvent findEvent, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
