using SongStash.Domain.ExternalReferences;
using Microsoft.Data.Sqlite;
using SongStash.Application.Importing;
using SongStash.Domain.Finds;
using SongStash.Infrastructure.Importing.Shazam;
using Xunit;

namespace SongStash.Infrastructure.Tests.Importing;

public sealed class ShazamMacLocalReaderTests
{
    private const double TestTimestamp = 789012345.25;

    [Fact]
    public async Task ValidRowMapsMetadataTimestampAndExternalReferences()
    {
        using var database = TestDatabase.Create();
        database.InsertRow(
            title: "Example title",
            artist: "Example artist",
            album: "Example album",
            artworkUrl: "https://example.test/artwork.png",
            date: TestTimestamp,
            recognitionId: "recognition-1",
            appleMusicId: "apple-123",
            isrc: "USAAA0000001",
            shazamKey: "shazam-456",
            shazamUrl: "https://www.shazam.com/track/456");

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(ImportReadStatus.Success, result.Status);
        Assert.Equal("Example title", candidate.Title);
        Assert.Equal("Example artist", candidate.Artist);
        Assert.Equal("Example album", candidate.Album);
        Assert.Equal(new Uri("https://example.test/artwork.png"), candidate.ArtworkUrl);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(TestTimestamp + 978307200), candidate.FoundAt);
        Assert.Equal("recognition-1", candidate.SourceItemId);
        Assert.Equal(FindSource.ShazamApple, candidate.Source);
        Assert.Collection(candidate.ExternalReferences,
            item => Assert.Equal((ExternalReferenceProvider.AppleMusic, "apple-123"), (item.Provider, item.Value)),
            item => Assert.Equal((ExternalReferenceProvider.Isrc, "USAAA0000001"), (item.Provider, item.Value)),
            item =>
            {
                Assert.Equal(ExternalReferenceProvider.Shazam, item.Provider);
                Assert.Equal("shazam-456", item.Value);
                Assert.Equal(new Uri("https://www.shazam.com/track/456"), item.Url);
            });
        Assert.Empty(candidate.Diagnostics);
    }

    [Fact]
    public async Task NullOptionalFieldsDoNotPreventCandidateCreation()
    {
        using var database = TestDatabase.Create();
        database.InsertRow(title: "Title", artist: "Artist", date: TestTimestamp);

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        var candidate = Assert.Single(result.Candidates);
        Assert.Null(candidate.Album);
        Assert.Null(candidate.ArtworkUrl);
        Assert.Null(candidate.SourceItemId);
        Assert.Empty(candidate.ExternalReferences);
    }

    [Fact]
    public async Task InvalidArtworkAndTimestampBecomeWarningsAndNullValues()
    {
        using var database = TestDatabase.Create();
        database.InsertRow(title: "Title", artist: "Artist", artworkUrl: "not a uri", date: "not a number");

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        var candidate = Assert.Single(result.Candidates);
        Assert.Null(candidate.ArtworkUrl);
        Assert.Null(candidate.FoundAt);
        Assert.Contains(candidate.Diagnostics, item => item.Code == "invalid-artwork-url");
        Assert.Contains(candidate.Diagnostics, item => item.Code == "invalid-timestamp");
    }

    [Fact]
    public async Task MissingTimestampProducesNullAndWarning()
    {
        using var database = TestDatabase.Create();
        database.InsertRow(title: "Title", artist: "Artist", date: null);

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        var candidate = Assert.Single(result.Candidates);
        Assert.Null(candidate.FoundAt);
        Assert.Contains(candidate.Diagnostics, item => item.Code == "invalid-timestamp");
    }

    [Fact]
    public async Task MissingTitleOrArtistKeepsCandidateAndAddsWarning()
    {
        using var database = TestDatabase.Create();
        database.InsertRow(title: null, artist: "Artist", date: TestTimestamp);

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(string.Empty, candidate.Title);
        Assert.Equal("Artist", candidate.Artist);
        Assert.Contains(candidate.Diagnostics, item => item.Code == "incomplete-required-metadata");
    }

    [Fact]
    public async Task MissingDatabaseReturnsUnavailable()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"shazam-missing-{Guid.NewGuid():N}.sqlite");

        var result = await new ShazamMacLocalReader(missingPath).ReadAsync();

        Assert.Equal(ImportReadStatus.Unavailable, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == "source-unavailable");
    }

    [Fact]
    public async Task MissingHistoryTableReturnsUnsupportedSource()
    {
        using var database = TestDatabase.CreateWithoutTable();

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        Assert.Equal(ImportReadStatus.UnsupportedSource, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == "unsupported-schema");
    }

    [Fact]
    public async Task MissingCriticalColumnReturnsUnsupportedSource()
    {
        using var database = TestDatabase.Create("CREATE TABLE ZSHTRACKMO (ZTITLE TEXT, ZDATE REAL);");

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        Assert.Equal(ImportReadStatus.UnsupportedSource, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == "unsupported-schema");
    }

    [Fact]
    public async Task MissingOptionalColumnsStillReadsSupportedSchema()
    {
        using var database = TestDatabase.Create("CREATE TABLE ZSHTRACKMO (ZTITLE TEXT, ZSUBTITLE TEXT, ZDATE REAL);");
        database.InsertMinimalRow("Title", "Artist", TestTimestamp);

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        Assert.Equal(ImportReadStatus.Success, result.Status);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("Title", candidate.Title);
        Assert.Equal("Artist", candidate.Artist);
        Assert.Empty(candidate.ExternalReferences);
    }

    [Fact]
    public async Task RepeatedTrackRowsRemainSeparateCandidatesWithRecognitionIds()
    {
        using var database = TestDatabase.Create();
        database.InsertRow("Same title", "Same artist", date: TestTimestamp, recognitionId: "event-a", shazamKey: "same-track");
        database.InsertRow("Same title", "Same artist", date: TestTimestamp + 120, recognitionId: "event-b", shazamKey: "same-track");

        var result = await new ShazamMacLocalReader(database.Path).ReadAsync();

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(new[] { "event-a", "event-b" }, result.Candidates.Select(candidate => candidate.SourceItemId));
        Assert.Equal(2, result.Candidates.SelectMany(candidate => candidate.ExternalReferences)
            .Count(reference => reference.Provider == ExternalReferenceProvider.Shazam && reference.Value == "same-track"));
        Assert.Equal(2, result.Candidates.Select(candidate => candidate.FoundAt).Distinct().Count());
        Assert.All(result.Candidates, candidate => Assert.Equal(FindSource.ShazamApple, candidate.Source));
    }

    private sealed class TestDatabase : IDisposable
    {
        private const string FullSchema = """
            CREATE TABLE ZSHTRACKMO (
                Z_PK INTEGER PRIMARY KEY,
                ZTITLE TEXT,
                ZSUBTITLE TEXT,
                ZDATE REAL,
                ZALBUMNAME TEXT,
                ZARTWORKURL TEXT,
                ZRECOGNITIONID TEXT,
                ZAPPLEMUSICID TEXT,
                ZISRC TEXT,
                ZSHAZAMKEY TEXT,
                ZSHAZAMURL TEXT
            );
            """;

        private readonly string _directory;
        private readonly string _connectionString;

        private TestDatabase(string directory)
        {
            _directory = directory;
            Path = System.IO.Path.Combine(directory, "fixture.sqlite");
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
        }

        public string Path { get; }

        public static TestDatabase Create(string? schema = null)
        {
            var database = New();
            using var connection = new SqliteConnection(database._connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = schema ?? FullSchema;
            command.ExecuteNonQuery();
            return database;
        }

        public static TestDatabase CreateWithoutTable()
        {
            var database = New();
            File.WriteAllBytes(database.Path, []);
            return database;
        }

        public void InsertRow(
            string? title = null,
            string? artist = null,
            string? album = null,
            string? artworkUrl = null,
            object? date = null,
            string? recognitionId = null,
            string? appleMusicId = null,
            string? isrc = null,
            string? shazamKey = null,
            string? shazamUrl = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ZSHTRACKMO
                    (ZTITLE, ZSUBTITLE, ZALBUMNAME, ZARTWORKURL, ZDATE, ZRECOGNITIONID,
                     ZAPPLEMUSICID, ZISRC, ZSHAZAMKEY, ZSHAZAMURL)
                VALUES
                    ($title, $artist, $album, $artworkUrl, $date, $recognitionId,
                     $appleMusicId, $isrc, $shazamKey, $shazamUrl);
                """;
            Add(command, "$title", title);
            Add(command, "$artist", artist);
            Add(command, "$album", album);
            Add(command, "$artworkUrl", artworkUrl);
            Add(command, "$date", date);
            Add(command, "$recognitionId", recognitionId);
            Add(command, "$appleMusicId", appleMusicId);
            Add(command, "$isrc", isrc);
            Add(command, "$shazamKey", shazamKey);
            Add(command, "$shazamUrl", shazamUrl);
            command.ExecuteNonQuery();
        }

        public void InsertMinimalRow(string title, string artist, double date)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO ZSHTRACKMO (ZTITLE, ZSUBTITLE, ZDATE) VALUES ($title, $artist, $date);";
            Add(command, "$title", title);
            Add(command, "$artist", artist);
            Add(command, "$date", date);
            command.ExecuteNonQuery();
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        private static TestDatabase New()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"songstash-shazam-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return new TestDatabase(directory);
        }

        private static void Add(SqliteCommand command, string name, object? value) =>
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
