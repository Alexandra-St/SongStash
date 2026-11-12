using SongStash.Application.Persistence;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;

namespace SongStash.Infrastructure.Persistence;

/// <summary>Commits manual song creation and attached finds in item-scoped transactions.</summary>
public sealed class EfManualEntryCommitter(SongStashDbContext dbContext) : IManualEntryCommitter
{
    public Task CommitNewSongAsync(Song song, FindEvent findEvent, CancellationToken cancellationToken = default) =>
        InTransactionAsync(async () =>
        {
            await dbContext.Songs.AddAsync(song, cancellationToken).ConfigureAwait(false);
            await dbContext.FindEvents.AddAsync(findEvent, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task CommitFindEventAsync(FindEvent findEvent, CancellationToken cancellationToken = default) =>
        InTransactionAsync(async () =>
        {
            await dbContext.FindEvents.AddAsync(findEvent, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private async Task InTransactionAsync(Func<Task> write, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await write().ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
