using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Domain.Tags;
using SongStash.Infrastructure.Persistence;
using SongStash.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SongStash.Infrastructure.Tests.Persistence;

public sealed class SongStashPersistenceTests
{
    private static readonly DateTimeOffset ImportedAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitialMigrationCreatesEmptyDatabaseSchema()
    {
        await using var database = TemporaryDatabase.Create();
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            Assert.Contains(await context.Database.GetAppliedMigrationsAsync(), migration => migration.EndsWith("_InitialLibrarySchema", StringComparison.Ordinal));
        }

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Songs','FindEvents','ExternalReferences','Tags');";
        Assert.Equal(4L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task SongRoundTripsThroughSqlite()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist", "Album", new Uri("https://example.test/art.png"), "Genre", "Notes");
        song.SetFavorite(true);

        await using (var context = database.CreateContext())
        {
            context.Songs.Add(song);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            var loaded = await context.Songs.SingleAsync();
            Assert.Equal(song.Id, loaded.Id);
            Assert.Equal(song.Title, loaded.Title);
            Assert.Equal(song.Artist, loaded.Artist);
            Assert.True(loaded.IsFavorite);
            Assert.Equal(song.CreatedAt, loaded.CreatedAt);
            Assert.Equal(song.UpdatedAt, loaded.UpdatedAt);
        }
    }

    [Fact]
    public async Task SongNullableMetadataRoundTrips()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using var context = database.CreateContext();
        context.Songs.Add(new Song("Title", "Artist"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await context.Songs.SingleAsync();
        Assert.Null(loaded.Album);
        Assert.Null(loaded.ArtworkUrl);
        Assert.Null(loaded.Genre);
        Assert.Null(loaded.Notes);
    }

    [Fact]
    public async Task FindEventWithFoundAtAndSourceItemIdRoundTrips()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        var foundAt = new DateTimeOffset(2026, 10, 26, 11, 40, 0, TimeSpan.Zero);
        var find = new FindEvent(song.Id, FindSource.ShazamApple, ImportedAt, foundAt, "event-id");

        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.FindEvents.Add(find);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await context.FindEvents.SingleAsync();
        Assert.Equal(foundAt, loaded.FoundAt);
        Assert.Equal("event-id", loaded.SourceItemId);
        Assert.Equal(FindSource.ShazamApple, loaded.Source);
    }

    [Fact]
    public async Task FindEventWithNullFoundAtRoundTrips()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");

        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.FindEvents.Add(new FindEvent(song.Id, FindSource.Manual, ImportedAt));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Null((await context.FindEvents.SingleAsync()).FoundAt);
    }

    [Fact]
    public async Task MultipleFindEventsForSongRemainSeparate()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.FindEvents.AddRange(
            new FindEvent(song.Id, FindSource.ShazamApple, ImportedAt, ImportedAt.AddMinutes(-5), "event-a"),
            new FindEvent(song.Id, FindSource.ShazamApple, ImportedAt, ImportedAt, "event-b"));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.FindEvents.CountAsync(find => find.SongId == song.Id));
    }

    [Fact]
    public async Task ExternalReferenceWithIdRoundTrips()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "track-key");

        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.ExternalReferences.Add(reference);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await context.ExternalReferences.SingleAsync();
        Assert.Equal("track-key", loaded.ExternalId);
        Assert.Null(loaded.Url);
    }

    [Fact]
    public async Task ExternalReferenceWithUrlRoundTrips()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        var url = new Uri("https://example.test/track");

        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.ExternalReferences.Add(new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, url: url));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(url, (await context.ExternalReferences.SingleAsync()).Url);
    }

    [Fact]
    public async Task ExternalReferenceWithIdAndUrlRoundTrips()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        var url = new Uri("https://example.test/track");

        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.ExternalReferences.Add(new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "track-key", url));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await context.ExternalReferences.SingleAsync();
        Assert.Equal("track-key", loaded.ExternalId);
        Assert.Equal(url, loaded.Url);
    }

    [Fact]
    public async Task TagAndNormalizedNameRoundTrip()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using var context = database.CreateContext();
        context.Tags.Add(new Tag("Road Trip"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await context.Tags.SingleAsync();
        Assert.Equal("Road Trip", loaded.Name);
        Assert.Equal("ROAD TRIP", loaded.NormalizedName);
    }

    [Fact]
    public async Task DeletingSongCascadeDeletesFindEvents()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        await using (var context = database.CreateContext())
        {
            context.Songs.Add(song);
            context.FindEvents.Add(new FindEvent(song.Id, FindSource.Manual, ImportedAt));
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            await new SongRepository(context).DeleteAsync(song.Id);
            Assert.Empty(await context.FindEvents.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task DeletingSongCascadeDeletesExternalReferences()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        await using (var context = database.CreateContext())
        {
            context.Songs.Add(song);
            context.ExternalReferences.Add(new ExternalReference(song.Id, ExternalReferenceProvider.Isrc, "USAAA0000001"));
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            await new SongRepository(context).DeleteAsync(song.Id);
            Assert.Empty(await context.ExternalReferences.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task DuplicateArtistAndTitleSongsAreAllowed()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using var context = database.CreateContext();
        context.Songs.AddRange(new Song("Song", "Artist"), new Song("Song", "Artist"));

        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Songs.CountAsync());
    }

    [Fact]
    public async Task RepeatRecognitionEventsForSameSongAreAllowed()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Song", "Artist");
        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.FindEvents.AddRange(
            new FindEvent(song.Id, FindSource.ShazamApple, ImportedAt, ImportedAt.AddMinutes(-1), "event-a"),
            new FindEvent(song.Id, FindSource.ShazamApple, ImportedAt, ImportedAt, "event-b"));

        await context.SaveChangesAsync();

        Assert.Equal(2, await context.FindEvents.CountAsync(find => find.SongId == song.Id));
    }

    [Fact]
    public async Task RepositoriesProvideSongsAndExternalReferencesForMatching()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var firstSong = new Song("First", "Artist");
        var secondSong = new Song("Second", "Artist");
        await using (var context = database.CreateContext())
        {
            var songs = new SongRepository(context);
            await songs.AddAsync(firstSong);
            await songs.AddAsync(secondSong);
            var references = new ExternalReferenceRepository(context);
            await references.AddAsync(new ExternalReference(firstSong.Id, ExternalReferenceProvider.Shazam, "key-1"));
            await references.AddAsync(new ExternalReference(secondSong.Id, ExternalReferenceProvider.Isrc, "USAAA0000002"));
        }

        await using (var context = database.CreateContext())
        {
            var songs = await new SongRepository(context).GetAllForMatchingAsync();
            var references = await new ExternalReferenceRepository(context)
                .GetBySongIdsAsync(songs.Select(song => song.Id).ToArray());
            Assert.Equal(2, songs.Count);
            Assert.Equal(2, references.Count);
        }
    }

    [Fact]
    public async Task FindEventRepositoryReturnsEventsOnlyForRequestedSong()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var firstSong = new Song("First", "Artist");
        var secondSong = new Song("Second", "Artist");
        await using (var context = database.CreateContext())
        {
            context.Songs.AddRange(firstSong, secondSong);
            context.FindEvents.AddRange(
                new FindEvent(firstSong.Id, FindSource.Manual, ImportedAt),
                new FindEvent(secondSong.Id, FindSource.Manual, ImportedAt));
            await context.SaveChangesAsync();
        }

        await using var readContext = database.CreateContext();
        var events = await new FindEventRepository(readContext).GetBySongIdAsync(firstSong.Id);

        Assert.Single(events);
        Assert.All(events, find => Assert.Equal(firstSong.Id, find.SongId));
    }

    [Fact]
    public async Task EnumValuesPersistAsReadableStringsAndRoundTrip()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        await using (var context = database.CreateContext())
        {
            context.Songs.Add(song);
            context.FindEvents.Add(new FindEvent(song.Id, FindSource.ShazamApple, ImportedAt));
            context.ExternalReferences.Add(new ExternalReference(song.Id, ExternalReferenceProvider.AppleMusic, "123"));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            Assert.Equal(FindSource.ShazamApple, (await context.FindEvents.SingleAsync()).Source);
            Assert.Equal(ExternalReferenceProvider.AppleMusic, (await context.ExternalReferences.SingleAsync()).Provider);
        }

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT Source FROM FindEvents LIMIT 1) || '|' || (SELECT Provider FROM ExternalReferences LIMIT 1);";
        Assert.Equal("ShazamApple|AppleMusic", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task UriValuesRoundTripAsStrings()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist", artworkUrl: new Uri("https://example.test/art.png"));
        var referenceUrl = new Uri("https://example.test/reference");
        await using var context = database.CreateContext();
        context.Songs.Add(song);
        context.ExternalReferences.Add(new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, url: referenceUrl));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(song.ArtworkUrl, (await context.Songs.SingleAsync()).ArtworkUrl);
        Assert.Equal(referenceUrl, (await context.ExternalReferences.SingleAsync()).Url);
    }

    [Fact]
    public async Task SongRepositorySupportsGetUpdateAndDelete()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Title", "Artist");
        await using (var context = database.CreateContext())
        {
            await new SongRepository(context).AddAsync(song);
        }

        await using (var context = database.CreateContext())
        {
            var repository = new SongRepository(context);
            var loaded = await repository.GetByIdAsync(song.Id);
            Assert.NotNull(loaded);
            loaded!.SetFavorite(true);
            await repository.UpdateAsync(loaded);
        }

        await using (var context = database.CreateContext())
        {
            var repository = new SongRepository(context);
            Assert.True((await repository.GetByIdAsync(song.Id))!.IsFavorite);
            await repository.DeleteAsync(song.Id);
            Assert.Null(await repository.GetByIdAsync(song.Id));
        }
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _directory;

        private TemporaryDatabase(string directory)
        {
            _directory = directory;
            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(directory, "songstash-tests.sqlite")
            }.ToString();
        }

        public string ConnectionString { get; }

        public static TemporaryDatabase Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"songstash-persistence-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return new TemporaryDatabase(directory);
        }

        public static async Task<TemporaryDatabase> CreateMigratedAsync()
        {
            var database = Create();
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }

        public SongStashDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<SongStashDbContext>().UseSqlite(ConnectionString).Options);

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
