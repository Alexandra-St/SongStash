using Microsoft.EntityFrameworkCore;
using SongStash.Application.Persistence;
using SongStash.Domain.Finds;

namespace SongStash.Infrastructure.Persistence.Repositories;

public sealed class FindEventRepository(SongStashDbContext dbContext) : IFindEventRepository
{
    public async Task<IReadOnlyList<FindEvent>> GetForSongsAsync(
        IReadOnlyCollection<Guid> songIds,
        CancellationToken cancellationToken = default)
    {
        if (songIds.Count == 0)
        {
            return [];
        }

        return await dbContext.FindEvents.AsNoTracking()
            .Where(find => songIds.Contains(find.SongId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FindEvent>> GetBySongIdAsync(
        Guid songId,
        CancellationToken cancellationToken = default) =>
        await dbContext.FindEvents.AsNoTracking()
            .Where(find => find.SongId == songId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(FindEvent findEvent, CancellationToken cancellationToken = default)
    {
        await dbContext.FindEvents.AddAsync(findEvent, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var findEvent = await dbContext.FindEvents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (findEvent is null) return;
        dbContext.FindEvents.Remove(findEvent);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
