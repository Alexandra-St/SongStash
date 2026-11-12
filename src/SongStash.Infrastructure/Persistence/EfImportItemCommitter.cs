using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;

namespace SongStash.Infrastructure.Persistence;

/// <summary>Wraps each item's repository writes in its own SQLite transaction.</summary>
public sealed class EfImportItemCommitter : IImportItemCommitter
{
    private readonly SongStashDbContext _dbContext;
    private readonly ISongRepository _songs;
    private readonly IExternalReferenceRepository _references;
    private readonly IFindEventRepository _findEvents;

    public EfImportItemCommitter(
        SongStashDbContext dbContext,
        ISongRepository songs,
        IExternalReferenceRepository references,
        IFindEventRepository findEvents)
    {
        _dbContext = dbContext;
        _songs = songs;
        _references = references;
        _findEvents = findEvents;
    }

    public Task CommitNewSongAsync(
        Song song,
        IReadOnlyCollection<ExternalReference> externalReferences,
        FindEvent findEvent,
        CancellationToken cancellationToken = default) =>
        InTransactionAsync(async () =>
        {
            await _songs.AddAsync(song, cancellationToken).ConfigureAwait(false);
            foreach (var reference in externalReferences)
            {
                await _references.AddAsync(reference, cancellationToken).ConfigureAwait(false);
            }

            await _findEvents.AddAsync(findEvent, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task CommitFindEventAsync(FindEvent findEvent, CancellationToken cancellationToken = default) =>
        InTransactionAsync(
            () => _findEvents.AddAsync(findEvent, cancellationToken),
            cancellationToken);

    private async Task InTransactionAsync(Func<Task> write, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await write().ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Clearing failed tracked entries prevents a later item's SaveChanges from retrying these writes.
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
