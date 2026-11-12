using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SongStash.Application.Exporting;
using SongStash.Application.Importing;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Infrastructure.Exporting;
using SongStash.Infrastructure.Persistence;
using SongStash.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SongStash.Infrastructure.Tests.Persistence;

public sealed class LibraryExportIntegrationTests
{
    private static readonly DateTimeOffset FixedExportTime = new(2026, 10, 26, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public async Task JsonExportsSongEventsReferencesMetadataEnumsAndNulls()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var song = new Song("Song Title", "Artist", "Album", new Uri("https://example.test/art.png"), "Genre", "Notes");
        var eventA = new FindEvent(song.Id, FindSource.ShazamApple, FixedExportTime,
            new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero), "recognition-a");
        var eventB = new FindEvent(song.Id, FindSource.ShazamApple, FixedExportTime, null, "recognition-b");
        var shazamUrl = new Uri("https://example.test/shazam/123");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.Add(song);
            seed.FindEvents.AddRange(eventA, eventB);
            seed.ExternalReferences.AddRange(
                new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "123", shazamUrl),
                new ExternalReference(song.Id, ExternalReferenceProvider.AppleMusic, "456"),
                new ExternalReference(song.Id, ExternalReferenceProvider.Isrc, "USAAA0000001"));
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = CreateService(context);
        await using var stream = new MemoryStream();
        await service.ExportJsonAsync(stream);
        var bytes = stream.ToArray();
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        Assert.Equal("SongStash.LibraryExport", root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(FixedExportTime, root.GetProperty("exportedAt").GetDateTimeOffset());
        Assert.Equal(1, root.GetProperty("songs").GetArrayLength());
        var songJson = root.GetProperty("songs")[0];
        Assert.Equal(song.Id, songJson.GetProperty("id").GetGuid());
        Assert.Equal("Song Title", songJson.GetProperty("title").GetString());
        Assert.Equal("Artist", songJson.GetProperty("artist").GetString());
        Assert.Equal("Album", songJson.GetProperty("album").GetString());
        Assert.Equal("https://example.test/art.png", songJson.GetProperty("artworkUrl").GetString());
        Assert.Equal("Genre", songJson.GetProperty("genre").GetString());
        Assert.Equal("Notes", songJson.GetProperty("notes").GetString());
        Assert.True(songJson.GetProperty("createdAt").GetString()!.Contains('T'));
        Assert.True(songJson.GetProperty("updatedAt").GetString()!.Contains('T'));
        Assert.Equal(2, songJson.GetProperty("findEvents").GetArrayLength());
        Assert.Contains(songJson.GetProperty("findEvents").EnumerateArray(), find =>
            find.GetProperty("source").GetString() == "ShazamApple" &&
            find.GetProperty("sourceItemId").GetString() == "recognition-a");
        Assert.Contains(songJson.GetProperty("findEvents").EnumerateArray(), find =>
            find.GetProperty("sourceItemId").GetString() == "recognition-b" &&
            find.GetProperty("foundAt").ValueKind == JsonValueKind.Null);
        Assert.Equal(3, songJson.GetProperty("externalReferences").GetArrayLength());
        Assert.Contains(songJson.GetProperty("externalReferences").EnumerateArray(), reference =>
            reference.GetProperty("provider").GetString() == "Shazam" &&
            reference.GetProperty("externalId").GetString() == "123" &&
            reference.GetProperty("url").GetString() == shazamUrl.AbsoluteUri);

        Assert.Equal(1, await context.Songs.CountAsync());
        Assert.Equal(2, await context.FindEvents.CountAsync());
        Assert.Equal(3, await context.ExternalReferences.CountAsync());
    }

    [Fact]
    public async Task JsonKeepsMultipleSongsAndDoesNotOmitSongWithoutFindEvents()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        var withEvents = new Song("Repeat", "Artist");
        var withoutEvents = new Song("No event", "Other artist");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.AddRange(withEvents, withoutEvents);
            seed.FindEvents.AddRange(
                new FindEvent(withEvents.Id, FindSource.ShazamApple, FixedExportTime,
                    FixedExportTime.AddMinutes(-3), "repeat-one"),
                new FindEvent(withEvents.Id, FindSource.ShazamApple, FixedExportTime,
                    FixedExportTime, "repeat-two"));
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        await using var stream = new MemoryStream();
        await CreateService(context).ExportJsonAsync(stream);
        using var document = JsonDocument.Parse(stream.ToArray());
        var songs = document.RootElement.GetProperty("songs").EnumerateArray().ToArray();
        Assert.Equal(2, songs.Length);
        var repeatedSong = Assert.Single(songs, item => item.GetProperty("id").GetGuid() == withEvents.Id);
        Assert.Equal(2, repeatedSong.GetProperty("findEvents").GetArrayLength());
        Assert.Equal(new[] { "repeat-one", "repeat-two" }, repeatedSong.GetProperty("findEvents")
            .EnumerateArray().Select(item => item.GetProperty("sourceItemId").GetString()));
        var songWithoutEvents = Assert.Single(songs, item => item.GetProperty("id").GetGuid() == withoutEvents.Id);
        Assert.Empty(songWithoutEvents.GetProperty("findEvents").EnumerateArray());
    }

    [Fact]
    public async Task CsvHasOneRowPerEventAndPreservesEscapedUnicodeFieldsAndExternalIds()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        const string title = "Song, \"Live\" 🎵";
        const string notes = "Line one\nВторая строка, \"цитата\" 😀";
        var song = new Song(title, "Артист, feat. Guest", null, null, null, notes);
        var noEventsSong = new Song("No finds", "Artist");
        var url = new Uri("https://example.test/shazam/track");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.AddRange(song, noEventsSong);
            seed.FindEvents.AddRange(
                new FindEvent(song.Id, FindSource.ShazamApple, FixedExportTime,
                    FixedExportTime.AddMinutes(-5), "recognition-a"),
                new FindEvent(song.Id, FindSource.ShazamApple, FixedExportTime,
                    FixedExportTime, "recognition-b"));
            seed.ExternalReferences.AddRange(
                new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "shazam-id", url),
                new ExternalReference(song.Id, ExternalReferenceProvider.AppleMusic, "apple-id"),
                new ExternalReference(song.Id, ExternalReferenceProvider.Isrc, "USAAA0000001"));
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        await using var stream = new MemoryStream();
        await CreateService(context).ExportCsvAsync(stream);
        var csv = new UTF8Encoding(false, true).GetString(stream.ToArray());
        var rows = ParseCsv(csv);

        Assert.Equal(4, rows.Count); // Header, two events, and one song without events.
        Assert.Equal("SongId", rows[0][0]);
        Assert.Equal(16, rows[0].Count);
        var eventRows = rows.Skip(1).Where(row => row[0] == song.Id.ToString()).ToArray();
        Assert.Equal(2, eventRows.Length);
        Assert.Equal(new[] { "recognition-a", "recognition-b" }, eventRows.Select(row => row[11]));
        Assert.All(eventRows, row =>
        {
            Assert.Equal(title, row[1]);
            Assert.Equal("Артист, feat. Guest", row[2]);
            Assert.Equal(notes, row[6]);
            Assert.Equal("shazam-id", row[12]);
            Assert.Equal(url.AbsoluteUri, row[13]);
            Assert.Equal("apple-id", row[14]);
            Assert.Equal("USAAA0000001", row[15]);
        });
        var noEventRow = Assert.Single(rows.Skip(1), row => row[0] == noEventsSong.Id.ToString());
        Assert.Equal("No finds", noEventRow[1]);
        Assert.Equal(string.Empty, noEventRow[8]);
        Assert.Equal(string.Empty, noEventRow[9]);
        Assert.Equal(string.Empty, noEventRow[10]);
        Assert.Equal(string.Empty, noEventRow[11]);
        Assert.Contains("\"Song, \"\"Live\"\" 🎵\"", csv);
        Assert.Contains("\"Line one\nВторая строка, \"\"цитата\"\" 😀\"", csv);
    }

    [Fact]
    public async Task CsvEmptyLibraryStillWritesHeaderAndEmptyExportPreservesNullMetadata()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using (var context = database.CreateContext())
        {
            await using var jsonStream = new MemoryStream();
            await CreateService(context).ExportJsonAsync(jsonStream);
            using var document = JsonDocument.Parse(jsonStream.ToArray());
            Assert.Empty(document.RootElement.GetProperty("songs").EnumerateArray());

            await using var csvStream = new MemoryStream();
            await CreateService(context).ExportCsvAsync(csvStream);
            var text = Encoding.UTF8.GetString(csvStream.ToArray());
            Assert.Equal(2, text.Split("\r\n", StringSplitOptions.None).Length);
            Assert.Contains("SongId,Title,Artist", text);
        }

        var song = new Song("Only song", "Artist");
        await using (var seed = database.CreateContext())
        {
            seed.Songs.Add(song);
            await seed.SaveChangesAsync();
        }

        await using var readContext = database.CreateContext();
        await using var stream = new MemoryStream();
        await CreateService(readContext).ExportJsonAsync(stream);
        using var populated = JsonDocument.Parse(stream.ToArray());
        var exportedSong = populated.RootElement.GetProperty("songs")[0];
        Assert.Equal(JsonValueKind.Null, exportedSong.GetProperty("album").ValueKind);
        Assert.Equal(JsonValueKind.Null, exportedSong.GetProperty("artworkUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, exportedSong.GetProperty("genre").ValueKind);
        Assert.Equal(JsonValueKind.Null, exportedSong.GetProperty("notes").ValueKind);
        Assert.Empty(exportedSong.GetProperty("externalReferences").EnumerateArray());
    }

    [Fact]
    public async Task ExportHonorsCancellationAndDoesNotCloseDestinationStream()
    {
        await using var database = await TemporaryDatabase.CreateMigratedAsync();
        await using var context = database.CreateContext();
        await using var stream = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(context).ExportJsonAsync(stream, cancellation.Token));
        Assert.True(stream.CanWrite);
    }

    private static ILibraryExportService CreateService(SongStashDbContext context) => new LibraryExportService(
        new SongRepository(context),
        new FindEventRepository(context),
        new ExternalReferenceRepository(context),
        new FixedClock(FixedExportTime));

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character == '\n' && !inQuotes)
            {
                if (field.Length > 0 && field[^1] == '\r')
                {
                    field.Length--;
                }

                row.Add(field.ToString());
                rows.Add(row);
                row = [];
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _directory;

        private TemporaryDatabase(string directory)
        {
            _directory = directory;
            ConnectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(directory, "export-tests.sqlite")
            }.ToString();
        }

        public string ConnectionString { get; }

        public static async Task<TemporaryDatabase> CreateMigratedAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"songstash-export-{Guid.NewGuid():N}");
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
