using Microsoft.EntityFrameworkCore;
using SongStash.Application.Importing;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Infrastructure.Persistence;
using SongStash.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SongStash.Infrastructure.Tests.Persistence;

public sealed class ImportServiceIntegrationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NewCandidateAtomicallyAddsSongReferencesAndEventWithClockValues()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var url = new Uri("https://example.test/track");
        var candidate = Candidate("New song", "Artist", "recognition-1") with
        {
            Album = "Album",
            ArtworkUrl = new Uri("https://example.test/art.png"),
            ExternalReferences =
            [
                new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key", url),
                new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key", url),
                new ExternalReferenceCandidate(ExternalReferenceProvider.AppleMusic, "123")
            ]
        };

        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context).ImportAsync([Preview(candidate, ImportPreviewStatus.New)]);
            Assert.Equal(1, result.AddedSongs);
            Assert.Equal(1, result.AddedFindEvents);
        }

        await using var verify = database.CreateContext();
        var song = await verify.Songs.SingleAsync();
        var find = await verify.FindEvents.SingleAsync();
        var references = await verify.ExternalReferences.OrderBy(reference => reference.Provider).ToListAsync();
        Assert.Equal("Album", song.Album);
        Assert.Equal(new Uri("https://example.test/art.png"), song.ArtworkUrl);
        Assert.Equal(FixedNow, song.CreatedAt);
        Assert.Equal(FixedNow, song.UpdatedAt);
        Assert.Null(find.FoundAt);
        Assert.Equal(FixedNow, find.ImportedAt);
        Assert.Equal("recognition-1", find.SourceItemId);
        Assert.Equal(2, references.Count);
        Assert.Contains(references, reference => reference.Provider == ExternalReferenceProvider.Shazam &&
            reference.ExternalId == "track-key" && reference.Url == url);
    }

    [Fact]
    public async Task ExistingSongGetsNewEventWithoutEnrichmentOrSecondSong()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Original title", "Original artist", "Original album",
            new Uri("https://example.test/original.png"), "Original genre", "Original notes");
        song.SetFavorite(true);
        await using (var seed = database.CreateContext())
        {
            seed.Songs.Add(song);
            await seed.SaveChangesAsync();
        }

        var candidate = Candidate("Changed title", "Changed artist", "new-event", FixedNow.AddMinutes(-1)) with
        {
            Album = "Candidate album",
            ArtworkUrl = new Uri("https://example.test/candidate.png"),
            ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Isrc, "USAAA0000001")]
        };
        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context).ImportAsync(
                [Preview(candidate, ImportPreviewStatus.AlreadyInLibrary, song.Id)]);
            Assert.Equal(0, result.AddedSongs);
            Assert.Equal(1, result.AddedFindEvents);
        }

        await using var verify = database.CreateContext();
        var stored = await verify.Songs.SingleAsync();
        Assert.Equal("Original title", stored.Title);
        Assert.Equal("Original artist", stored.Artist);
        Assert.Equal("Original album", stored.Album);
        Assert.Equal("Original genre", stored.Genre);
        Assert.Equal("Original notes", stored.Notes);
        Assert.Equal(new Uri("https://example.test/original.png"), stored.ArtworkUrl);
        Assert.True(stored.IsFavorite);
        Assert.Empty(await verify.ExternalReferences.ToListAsync());
        Assert.Single(await verify.FindEvents.ToListAsync());
    }

    [Fact]
    public async Task DifferentRecognitionIdsForSameTrackCreateTwoEventsOnOneSong()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Repeated song", "Artist");
        var key = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "same-track-key");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.Add(song);
            seed.ExternalReferences.Add(key);
            await seed.SaveChangesAsync();
        }

        var first = Candidate("Repeated song", "Artist", "recognition-a", FixedNow.AddMinutes(-8)) with
        {
            ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "same-track-key")]
        };
        var second = Candidate("Repeated song", "Artist", "recognition-b", FixedNow) with
        {
            ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "same-track-key")]
        };
        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context).ImportAsync(
            [
                Preview(first, ImportPreviewStatus.AlreadyInLibrary, song.Id),
                Preview(second, ImportPreviewStatus.AlreadyInLibrary, song.Id)
            ]);
            Assert.Equal(0, result.AddedSongs);
            Assert.Equal(2, result.AddedFindEvents);
        }

        await using var verify = database.CreateContext();
        Assert.Single(await verify.Songs.ToListAsync());
        var events = (await verify.FindEvents.ToListAsync()).OrderBy(find => find.FoundAt).ToArray();
        Assert.Equal(new[] { "recognition-a", "recognition-b" }, events.Select(find => find.SourceItemId));
        Assert.Equal(2, events.Select(find => find.FoundAt).Distinct().Count());
    }

    [Fact]
    public async Task DuplicateCountsAsSkippedAndDoesNotAddEvent()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Song", "Artist");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.Add(song);
            seed.FindEvents.Add(new FindEvent(song.Id, FindSource.ShazamApple, FixedNow,
                FixedNow.AddMinutes(-2), "same-event"));
            await seed.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context).ImportAsync([
                Preview(Candidate("Song", "Artist", "same-event", FixedNow), ImportPreviewStatus.AlreadyInLibrary, song.Id)
            ]);
            Assert.Equal(1, result.SkippedDuplicates);
            Assert.Equal(0, result.AddedFindEvents);
        }

        await using var verify = database.CreateContext();
        Assert.Single(await verify.FindEvents.ToListAsync());
    }

    [Fact]
    public async Task IndeterminateIncompleteAndNeedsReviewDoNotWrite()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Existing", "Artist");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.Add(song);
            await seed.SaveChangesAsync();
        }

        var items = new[]
        {
            Preview(Candidate("Existing", "Artist"), ImportPreviewStatus.AlreadyInLibrary, song.Id),
            Preview(Candidate(" ", "Artist"), ImportPreviewStatus.Incomplete),
            Preview(Candidate("Ambiguous", "Artist"), ImportPreviewStatus.NeedsReview)
        };
        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context).ImportAsync(items);
            Assert.Equal(3, result.NeedsAttention);
        }

        await using var verify = database.CreateContext();
        Assert.Single(await verify.Songs.ToListAsync());
        Assert.Empty(await verify.FindEvents.ToListAsync());
        Assert.Empty(await verify.ExternalReferences.ToListAsync());
    }

    [Fact]
    public async Task TwoNewItemsProduceSeparateSongAndEventCounts()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context).ImportAsync(
            [
                Preview(Candidate("First", "Artist"), ImportPreviewStatus.New),
                Preview(Candidate("Second", "Artist"), ImportPreviewStatus.New)
            ]);
            Assert.Equal(2, result.AddedSongs);
            Assert.Equal(2, result.AddedFindEvents);
            Assert.Equal(0, result.Failed);
        }

        await using var verify = database.CreateContext();
        Assert.Equal(2, await verify.Songs.CountAsync());
        Assert.Equal(2, await verify.FindEvents.CountAsync());
    }

    [Fact]
    public async Task FailedMiddleItemRollsBackOnlyItAndLaterItemCanCommit()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER FailSpecificFindEvent BEFORE INSERT ON FindEvents
                WHEN NEW.SourceItemId = 'broken-event'
                BEGIN SELECT RAISE(ABORT, 'fixture failure'); END;
                """);
        }

        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context).ImportAsync(
            [
                Preview(Candidate("First", "Artist", "first-event") with
                {
                    ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.AppleMusic, "first-id")]
                }, ImportPreviewStatus.New),
                Preview(Candidate("Broken", "Artist", "broken-event") with
                {
                    ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "broken-key")]
                }, ImportPreviewStatus.New),
                Preview(Candidate("Last", "Artist", "last-event") with
                {
                    ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.AppleMusic, "last-id")]
                }, ImportPreviewStatus.New)
            ]);
            Assert.Equal(2, result.AddedSongs);
            Assert.Equal(2, result.AddedFindEvents);
            Assert.Equal(1, result.Failed);
            Assert.Contains("Could not save", result.Items[1].Message);
        }

        await using var verify = database.CreateContext();
        Assert.Equal(new[] { "First", "Last" }, (await verify.Songs.OrderBy(song => song.Title).ToListAsync())
            .Select(song => song.Title));
        Assert.Equal(2, await verify.ExternalReferences.CountAsync());
        Assert.Equal(new[] { "first-event", "last-event" }, await verify.FindEvents.OrderBy(find => find.SourceItemId)
            .Select(find => find.SourceItemId).ToArrayAsync());
    }

    [Fact]
    public async Task FailedNewSongLeavesNoOrphanSongReferenceOrEvent()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER FailSpecificFindEvent BEFORE INSERT ON FindEvents
                WHEN NEW.SourceItemId = 'broken-event'
                BEGIN SELECT RAISE(ABORT, 'fixture failure'); END;
                """);
        }

        await using (var context = database.CreateContext())
        {
            var item = Preview(Candidate("Broken", "Artist", "broken-event") with
            {
                ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "key")]
            }, ImportPreviewStatus.New);
            var result = await CreateService(context).ImportAsync([item]);
            Assert.Equal(1, result.Failed);
        }

        await using var verify = database.CreateContext();
        Assert.Empty(await verify.Songs.ToListAsync());
        Assert.Empty(await verify.ExternalReferences.ToListAsync());
        Assert.Empty(await verify.FindEvents.ToListAsync());
    }

    [Fact]
    public async Task CancellationAfterSongWriteRollsBackCurrentItemAndStopsFollowingItems()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        using var cancellation = new CancellationTokenSource();
        await using (var context = database.CreateContext())
        {
            var songRepo = new SongRepository(context);
            var innerReferences = new ExternalReferenceRepository(context);
            var cancellingReferences = new CancelOnAddReferenceRepository(innerReferences, cancellation);
            var eventRepo = new FindEventRepository(context);
            var committer = new EfImportItemCommitter(context, songRepo, cancellingReferences, eventRepo);
            var service = CreateService(context, committer);
            var items = new[]
            {
                Preview(Candidate("Cancelled", "Artist", "cancel-event") with
                {
                    ExternalReferences = [new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "cancel-key")]
                }, ImportPreviewStatus.New),
                Preview(Candidate("Must not start", "Artist"), ImportPreviewStatus.New)
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ImportAsync(items, cancellation.Token));
        }

        await using var verify = database.CreateContext();
        Assert.Empty(await verify.Songs.ToListAsync());
        Assert.Empty(await verify.ExternalReferences.ToListAsync());
        Assert.Empty(await verify.FindEvents.ToListAsync());
    }

    private static ImportService CreateService(SongStashDbContext context, IImportItemCommitter? committer = null)
    {
        var songs = new SongRepository(context);
        var events = new FindEventRepository(context);
        var references = new ExternalReferenceRepository(context);
        return new ImportService(songs, events,
            committer ?? new EfImportItemCommitter(context, songs, references, events),
            new FindEventDeduplicator(), new FixedClock(FixedNow));
    }

    private static ImportPreviewItem Preview(
        ImportCandidate candidate,
        ImportPreviewStatus status,
        Guid? matchedSongId = null) =>
        new(candidate, status, matchedSongId, [], null, candidate.Diagnostics);

    private static ImportCandidate Candidate(
        string title,
        string artist,
        string? sourceItemId = null,
        DateTimeOffset? foundAt = null) =>
        new(title, artist, FindSource.ShazamApple)
        {
            SourceItemId = sourceItemId,
            FoundAt = foundAt
        };

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class CancelOnAddReferenceRepository(
        IExternalReferenceRepository inner,
        CancellationTokenSource cancellation) : IExternalReferenceRepository
    {
        public Task<IReadOnlyList<ExternalReference>> GetBySongIdsAsync(
            IReadOnlyCollection<Guid> songIds,
            CancellationToken cancellationToken = default) => inner.GetBySongIdsAsync(songIds, cancellationToken);

        public Task AddAsync(ExternalReference reference, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return inner.AddAsync(reference, cancellationToken);
        }
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _directory;

        private TemporaryDatabase(string directory)
        {
            _directory = directory;
            ConnectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(directory, "import-tests.sqlite")
            }.ToString();
        }

        public string ConnectionString { get; }

        public static async Task<TemporaryDatabase> CreateMigratedAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"songstash-import-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var database = new TemporaryDatabase(directory);
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
