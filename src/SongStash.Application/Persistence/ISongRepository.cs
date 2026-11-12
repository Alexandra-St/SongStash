using SongStash.Domain.Songs;

namespace SongStash.Application.Persistence;

public interface ISongRepository
{
    Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default);
    Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Song song, CancellationToken cancellationToken = default);
    Task UpdateAsync(Song song, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
