using Microsoft.EntityFrameworkCore;
using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Application.Persistence;
using SongStash.Application.Library;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Infrastructure.Exporting;
using SongStash.Infrastructure.Importing;
using SongStash.Infrastructure.Importing.Shazam;
using SongStash.Infrastructure.Persistence;
using SongStash.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SongStash.Infrastructure.Tests.Importing;

public sealed class FileImportPipelineIntegrationTests
{
    [Fact]
    public async Task ShazamFixtureFlowsThroughPreviewImportQueriesExportAndDuplicateReimport()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var shazamPath = Path.Combine(Path.GetTempPath(), $"songstash-shazam-pipeline-{Guid.NewGuid():N}.sqlite");
        await CreateShazamFixtureAsync(shazamPath);
        try
        {
            await using var context = database.CreateContext();
            var songs = new SongRepository(context);
            var references = new ExternalReferenceRepository(context);
            var events = new FindEventRepository(context);
            var import = new ImportService(songs, events,
                new EfImportItemCommitter(context, songs, references, events),
                new FindEventDeduplicator(), new SystemClock());
            var orchestrator = new ImportOrchestrator(new UnusedReader(), songs, references,
                new ImportPreviewBuilder(new SongMatcher()), import);
            var reader = new ShazamMacLocalReader(shazamPath);

            var preview = await orchestrator.ReadPreviewAsync(reader);
            Assert.Equal(ImportPreviewRunStatus.Success, preview.Status);
            Assert.Equal(2, preview.Items.Count);
            Assert.All(preview.Items, item => Assert.Equal(ImportPreviewStatus.New, item.Status));
            Assert.All(preview.Items, item => Assert.Equal("same-track", Assert.Single(item.Candidate.ExternalReferences,
                reference => reference.Value == "same-track").Value));
            Assert.Equal(2, preview.Items.Select(item => item.Candidate.SourceItemId).Distinct().Count());
            Assert.Equal(2, preview.Items.Select(item => item.Candidate.FoundAt).Distinct().Count());

            var firstImport = await orchestrator.ImportAsync(preview.Items);
            Assert.Equal(1, firstImport.AddedSongs);
            Assert.Equal(2, firstImport.AddedFindEvents);
            Assert.Equal(1, await context.Songs.CountAsync());
            Assert.Equal(2, await context.FindEvents.CountAsync());

            var library = new LibraryQueryService(songs, events, references);
            var libraryRow = Assert.Single(await library.GetSongsAsync());
            Assert.Equal(2, libraryRow.FindCount);
            var details = await library.GetSongDetailsAsync(libraryRow.Id);
            Assert.Equal(2, details!.FindEvents.Count);
            var overview = await new OverviewQueryService(context).GetOverviewAsync();
            Assert.Equal(1, overview.TotalSongs);
            Assert.Equal(2, overview.TotalFinds);

            var secondPreview = await orchestrator.ReadPreviewAsync(reader);
            Assert.Equal(2, secondPreview.Items.Count);
            var secondImport = await orchestrator.ImportAsync(secondPreview.Items);
            Assert.Equal(0, secondImport.AddedSongs);
            Assert.Equal(0, secondImport.AddedFindEvents);
            Assert.Equal(2, secondImport.SkippedDuplicates);
            Assert.Equal(1, await context.Songs.CountAsync());
            Assert.Equal(2, await context.FindEvents.CountAsync());

            await using var exported = new MemoryStream();
            await new LibraryExportService(songs, events, references, new SystemClock()).ExportJsonAsync(exported);
            Assert.Contains("recognition-a", System.Text.Encoding.UTF8.GetString(exported.ToArray()));
            Assert.Contains("recognition-b", System.Text.Encoding.UTF8.GetString(exported.ToArray()));
        }
        finally
        {
            if (File.Exists(shazamPath)) File.Delete(shazamPath);
        }
    }

    [Fact]
    public async Task CsvReaderFlowsThroughExistingPreviewImportServiceAndSqliteCommit()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var csvPath = Path.Combine(Path.GetTempPath(), $"songstash-file-import-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(csvPath,
            "Title,Artist,Source,SourceItemId,ShazamId\nImported 🎵,Artist,Manual,event-1,track-key\nImported 🎵,Artist,Manual,event-2,track-key\n,Artist,Manual,event-bad,track-key\n");
        try
        {
            await using var context = database.CreateContext();
            var songs = new SongRepository(context);
            var references = new ExternalReferenceRepository(context);
            var findEvents = new FindEventRepository(context);
            var service = new ImportService(songs, findEvents,
                new EfImportItemCommitter(context, songs, references, findEvents),
                new FindEventDeduplicator(), new SystemClock());
            var orchestrator = new ImportOrchestrator(new UnusedReader(), songs, references,
                new ImportPreviewBuilder(new SongMatcher()), service);

            var preview = await orchestrator.ReadPreviewAsync(new CsvImportSourceReader(csvPath));
            Assert.Equal(3, preview.Items.Count);
            Assert.Equal(2, preview.Items.Count(item => item.Status == ImportPreviewStatus.New));
            Assert.Equal(ImportPreviewStatus.Incomplete, preview.Items[2].Status);
            Assert.NotEmpty(preview.Items[2].Diagnostics);
            var result = await orchestrator.ImportAsync(preview.Items);

            Assert.Equal(1, result.AddedSongs);
            Assert.Equal(2, result.AddedFindEvents);
            Assert.Equal(1, result.NeedsAttention);
            Assert.Equal(1, await context.Songs.CountAsync());
            Assert.Equal(new[] { "event-1", "event-2" }, await context.FindEvents.OrderBy(item => item.SourceItemId)
                .Select(item => item.SourceItemId).ToArrayAsync());
            Assert.Equal(1, await context.ExternalReferences.CountAsync());

            var repeatedPreview = await orchestrator.ReadPreviewAsync(new CsvImportSourceReader(csvPath));
            var repeatedImport = await orchestrator.ImportAsync(repeatedPreview.Items);
            Assert.Equal(2, repeatedImport.SkippedDuplicates);
            Assert.Equal(1, await context.Songs.CountAsync());
            Assert.Equal(2, await context.FindEvents.CountAsync());
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task JsonFileFlowsThroughPreviewImportLibraryAndDuplicateReimport()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var jsonPath = Path.Combine(Path.GetTempPath(), $"songstash-file-import-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(jsonPath, """
            {"songs":[
              {"title":"Повтор 🎵","artist":"Артист","source":"Manual","sourceItemId":"json-a","shazamId":"json-track"},
              {"title":"Повтор 🎵","artist":"Артист","source":"Manual","sourceItemId":"json-b","shazamId":"json-track"},
              {"title":"","artist":"Artist","sourceItemId":"bad-row"}
            ]}
            """);
        try
        {
            await using var context = database.CreateContext();
            var songs = new SongRepository(context);
            var references = new ExternalReferenceRepository(context);
            var events = new FindEventRepository(context);
            var service = new ImportService(songs, events,
                new EfImportItemCommitter(context, songs, references, events), new FindEventDeduplicator(), new SystemClock());
            var orchestrator = new ImportOrchestrator(new UnusedReader(), songs, references,
                new ImportPreviewBuilder(new SongMatcher()), service);

            var preview = await orchestrator.ReadPreviewAsync(new JsonImportSourceReader(jsonPath));
            Assert.Equal(2, preview.Items.Count(item => item.Status == ImportPreviewStatus.New));
            Assert.Equal(ImportPreviewStatus.Incomplete, preview.Items[2].Status);
            var result = await orchestrator.ImportAsync(preview.Items);
            Assert.Equal(1, result.AddedSongs);
            Assert.Equal(2, result.AddedFindEvents);
            Assert.Single(await new LibraryQueryService(songs, events, references).GetSongsAsync());
            Assert.Equal(2, await context.FindEvents.CountAsync());

            var repeated = await orchestrator.ReadPreviewAsync(new JsonImportSourceReader(jsonPath));
            Assert.Equal(2, (await orchestrator.ImportAsync(repeated.Items)).SkippedDuplicates);
            Assert.Equal(1, await context.Songs.CountAsync());
            Assert.Equal(2, await context.FindEvents.CountAsync());
        }
        finally
        {
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
        }
    }

    [Fact]
    public async Task SyntheticLargeLibrarySupportsQueriesDetailsExportsAndFiveHundredCandidatePreview()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using (var seed = database.CreateContext())
        {
            var songs = Enumerable.Range(0, 5_000)
                .Select(index => new Song($"Song {index:D5}", $"Artist {index % 250:D3}", null, null, null, null, DateTimeOffset.UnixEpoch))
                .ToArray();
            var events = songs.SelectMany((song, index) => Enumerable.Range(0, index < 2_500 ? 3 : 2)
                .Select(eventIndex => new FindEvent(song.Id, FindSource.Manual, DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch.AddMinutes(index + eventIndex), $"event-{index}-{eventIndex}")))
                .ToArray();
            seed.Songs.AddRange(songs);
            seed.FindEvents.AddRange(events);
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var songsRepo = new SongRepository(context);
        var eventsRepo = new FindEventRepository(context);
        var referencesRepo = new ExternalReferenceRepository(context);
        var libraryQuery = new LibraryQueryService(songsRepo, eventsRepo, referencesRepo);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var library = await libraryQuery.GetSongsAsync();
        var overview = await new OverviewQueryService(context).GetOverviewAsync();
        Assert.Equal(5_000, library.Count);
        Assert.Equal(5_000, overview.TotalSongs);
        Assert.Equal(12_500, overview.TotalFinds);
        var details = await libraryQuery.GetSongDetailsAsync(library[0].Id);
        Assert.NotNull(details);
        Assert.Equal(3, details!.FindEvents.Count);

        var previewCandidates = Enumerable.Range(0, 500)
            .Select(index => new ImportCandidate($"Preview {index}", "New artist", FindSource.ImportedUnknown)).ToArray();
        var preview = new ImportPreviewBuilder(new SongMatcher()).Build(previewCandidates, [], []);
        Assert.Equal(500, preview.Count);
        Assert.All(preview, item => Assert.Equal(ImportPreviewStatus.New, item.Status));

        var exporter = new LibraryExportService(songsRepo, eventsRepo, referencesRepo, new SystemClock());
        await using var json = new MemoryStream();
        await exporter.ExportJsonAsync(json);
        Assert.True(json.Length > 0);
        await using var csv = new MemoryStream();
        await exporter.ExportCsvAsync(csv);
        Assert.True(csv.Length > 0);
        timer.Stop();
        Assert.True(timer.Elapsed < TimeSpan.FromMinutes(1), $"Synthetic 5,000-song smoke took {timer.Elapsed}.");
    }

    private sealed class UnusedReader : IImportSourceReader
    {
        public Task<ImportReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ImportReadResult.Success());
    }

    private static async Task CreateShazamFixtureAsync(string path)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = """
                CREATE TABLE ZSHTRACKMO (
                    Z_PK INTEGER PRIMARY KEY, ZTITLE TEXT, ZSUBTITLE TEXT, ZDATE REAL,
                    ZALBUMNAME TEXT, ZARTWORKURL TEXT, ZRECOGNITIONID TEXT,
                    ZAPPLEMUSICID TEXT, ZISRC TEXT, ZSHAZAMKEY TEXT, ZSHAZAMURL TEXT);
                """;
            await schema.ExecuteNonQueryAsync();
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO ZSHTRACKMO (ZTITLE,ZSUBTITLE,ZDATE,ZRECOGNITIONID,ZSHAZAMKEY)
            VALUES ('Repeat Song','Artist',789012345.25,'recognition-a','same-track'),
                   ('Repeat Song','Artist',789012346.25,'recognition-b','same-track');
            """;
        await insert.ExecuteNonQueryAsync();
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _directory;
        private TemporaryDatabase(string directory) => _directory = directory;
        public string ConnectionString => new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_directory, "file-import.sqlite")
        }.ToString();

        public static async Task<TemporaryDatabase> CreateMigratedAsync()
        {
            var database = new TemporaryDatabase(Path.Combine(Path.GetTempPath(), $"songstash-file-db-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(database._directory);
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }

        public SongStashDbContext CreateContext() => new(
            new DbContextOptionsBuilder<SongStashDbContext>().UseSqlite(ConnectionString).Options);

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
