using Microsoft.EntityFrameworkCore;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;

namespace SongStash.Infrastructure.Persistence.Repositories;

public sealed class ExternalReferenceRepository(SongStashDbContext dbContext) : IExternalReferenceRepository
{
    public async Task<IReadOnlyList<ExternalReference>> GetBySongIdsAsync(
        IReadOnlyCollection<Guid> songIds,
        CancellationToken cancellationToken = default)
    {
        if (songIds.Count == 0)
        {
            return [];
        }

        return await dbContext.ExternalReferences.AsNoTracking()
            .Where(reference => songIds.Contains(reference.SongId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ExternalReference reference, CancellationToken cancellationToken = default)
    {
        await dbContext.ExternalReferences.AddAsync(reference, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
