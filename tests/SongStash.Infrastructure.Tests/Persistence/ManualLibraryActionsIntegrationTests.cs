using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SongStash.Application.Importing;
using SongStash.Application.LibraryActions;
using SongStash.Application.Matching;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Infrastructure.Persistence;
using SongStash.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SongStash.Infrastructure.Tests.Persistence;

public sealed class ManualLibraryActionsIntegrationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ManualSongAndFindEventPersistAsOneAggregate()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var result = await CreateManualService(context).AddAsync(
            new ManualSongInput("Manual song", "Artist", "Album", "Jazz", "Notes", FixedNow.AddDays(-1)));
        Assert.Equal(ManualAddOutcome.AddedNewSong, result.Outcome);

        await using var verify = database.CreateContext();
        var song = await verify.Songs.SingleAsync();
        var find = await verify.FindEvents.SingleAsync();
        Assert.Equal(result.SongId, song.Id);
        Assert.Equal(FixedNow, song.CreatedAt);
        Assert.Equal(FixedNow, song.UpdatedAt);
        Assert.Equal(FindSource.Manual, find.Source);
        Assert.Equal(FixedNow, find.ImportedAt);
        Assert.Equal(FixedNow.AddDays(-1), find.FoundAt);
    }

    [Fact]
    public async Task ManualSongTransactionRollsBackSongWhenFindInsertFails()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await using (var seed = database.CreateContext())
        {
            await seed.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectManualFind BEFORE INSERT ON FindEvents BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        }

        await using (var context = database.CreateContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => CreateManualService(context)
                .AddAsync(new ManualSongInput("Must rollback", "Artist")));
        }

        await using var verify = database.CreateContext();
        Assert.Equal(0, await verify.Songs.CountAsync());
        Assert.Equal(0, await verify.FindEvents.CountAsync());
    }

    [Fact]
    public async Task FavoritePersistsAndFindDeletionLeavesSongUntilSongDeletionCascades()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var song = new Song("Song", "Artist");
        var firstFind = new FindEvent(song.Id, FindSource.Manual, FixedNow);
        var lastFind = new FindEvent(song.Id, FindSource.Manual, FixedNow.AddMinutes(1));
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "key");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.Add(song);
            seed.FindEvents.AddRange(firstFind, lastFind);
            seed.ExternalReferences.Add(reference);
            await seed.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var actions = new LibraryActionsService(new SongRepository(context), new FindEventRepository(context));
            Assert.True(await actions.SetFavoriteAsync(song.Id, true));
            await actions.DeleteFindEventAsync(firstFind.Id);
        }
        await using (var verify = database.CreateContext())
        {
            Assert.True((await verify.Songs.SingleAsync()).IsFavorite);
            Assert.Single(await verify.FindEvents.ToListAsync());
            Assert.Single(await verify.ExternalReferences.ToListAsync());
        }

        await using (var context = database.CreateContext())
        {
            var actions = new LibraryActionsService(new SongRepository(context), new FindEventRepository(context));
            await actions.DeleteFindEventAsync(lastFind.Id);
            Assert.NotNull(await new SongRepository(context).GetByIdAsync(song.Id));
            await actions.DeleteSongAsync(song.Id);
        }
        await using var final = database.CreateContext();
        Assert.Empty(await final.Songs.ToListAsync());
        Assert.Empty(await final.FindEvents.ToListAsync());
        Assert.Empty(await final.ExternalReferences.ToListAsync());
    }

    private static ManualSongService CreateManualService(SongStashDbContext context) =>
        new ManualSongService(new SongRepository(context), new ExternalReferenceRepository(context),
            new SongMatcher(), new EfManualEntryCommitter(context), new FixedClock());

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => FixedNow; }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"songstash-manual-{Guid.NewGuid():N}");
        private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = Path.Combine(_folder, "library.sqlite") }.ToString();

        public static async Task<TemporaryDatabase> CreateAsync()
        {
            var database = new TemporaryDatabase();
            Directory.CreateDirectory(database._folder);
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }

        public SongStashDbContext CreateContext() => new(new DbContextOptionsBuilder<SongStashDbContext>()
            .UseSqlite(ConnectionString).Options);

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }
    }
}
