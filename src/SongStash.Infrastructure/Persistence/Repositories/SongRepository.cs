using Microsoft.EntityFrameworkCore;
using SongStash.Application.Persistence;
using SongStash.Domain.Songs;

namespace SongStash.Infrastructure.Persistence.Repositories;

public sealed class SongRepository(SongStashDbContext dbContext) : ISongRepository
{
    public async Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Songs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Songs.AsNoTracking().SingleOrDefaultAsync(song => song.Id == id, cancellationToken);

    public async Task AddAsync(Song song, CancellationToken cancellationToken = default)
    {
        await dbContext.Songs.AddAsync(song, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Song song, CancellationToken cancellationToken = default)
    {
        dbContext.Songs.Update(song);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var song = await dbContext.Songs.SingleOrDefaultAsync(song => song.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (song is null)
        {
            return;
        }

        dbContext.Songs.Remove(song);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
