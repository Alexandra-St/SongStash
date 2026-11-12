using System.Text;
using SongStash.Application.Importing;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Infrastructure.Importing;
using Xunit;

namespace SongStash.Infrastructure.Tests.Importing;

public sealed class FileImportSourceReaderTests
{
    [Fact]
    public async Task CsvReadsMinimalRecordAndAliases()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("Song,Performer\nTitle,Artist\n");
        var result = await new CsvImportSourceReader(file.Path).ReadAsync();
        var item = Assert.Single(result.Candidates);
        Assert.Equal("Title", item.Title);
        Assert.Equal("Artist", item.Artist);
        Assert.Equal(FindSource.ImportedUnknown, item.Source);
    }

    [Fact]
    public async Task CsvReadsOptionalMetadataAndExactDateAndExternalReferences()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("Title,Artist,Album,ArtworkUrl,DateFound,Source,SourceItemId,ShazamId,ShazamUrl,AppleMusicId,Isrc\n" +
            "Song,Artist,Album,https://art.test/a.png,2026-02-03T10:20:30+02:00,ShazamAndroid,event-1,shazam-1,https://shazam.test/1,apple-1,ISRC-1\n");
        var item = Assert.Single((await new CsvImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Equal("Album", item.Album);
        Assert.Equal(new Uri("https://art.test/a.png"), item.ArtworkUrl);
        Assert.Equal(new DateTimeOffset(2026, 2, 3, 10, 20, 30, TimeSpan.FromHours(2)), item.FoundAt);
        Assert.Equal(FindSource.ShazamAndroid, item.Source);
        Assert.Equal("event-1", item.SourceItemId);
        Assert.Equal(3, item.ExternalReferences.Count);
        Assert.Contains(item.ExternalReferences, r => r.Provider == ExternalReferenceProvider.Shazam &&
            r.Value == "shazam-1" && r.Url == new Uri("https://shazam.test/1"));
        Assert.Contains(item.ExternalReferences, r => r.Provider == ExternalReferenceProvider.AppleMusic && r.Value == "apple-1");
        Assert.Contains(item.ExternalReferences, r => r.Provider == ExternalReferenceProvider.Isrc && r.Value == "ISRC-1");
    }

    [Theory]
    [InlineData("Song,Artist\n\"Song, Live\",Artist\n", "Song, Live")]
    [InlineData("Title,Artist\n\"Song \"\"Live\"\"\",Artist\n", "Song \"Live\"")]
    [InlineData("Title,Artist\n\"Музыка 🎵\",Исполнитель 😀\n", "Музыка 🎵")]
    public async Task CsvSupportsQuotedFieldsAndUnicode(string csv, string expectedTitle)
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync(csv);
        var result = await new CsvImportSourceReader(file.Path).ReadAsync();
        Assert.Equal(expectedTitle, Assert.Single(result.Candidates).Title);
    }

    [Fact]
    public async Task CsvPreservesNewlinesInsideQuotedField()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("Title,Artist,Album\nSong,Artist,\"Line one\nLine two\"\n");
        var item = Assert.Single((await new CsvImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Equal("Line one\nLine two", item.Album);
    }

    [Theory]
    [InlineData("Artist\nArtist\n", "csv.columns.missing")]
    [InlineData("Title\nSong\n", "csv.columns.missing")]
    [InlineData("Title,Artist\n\"broken,Artist\n", "csv.malformed")]
    [InlineData("", "csv.empty")]
    public async Task CsvReturnsControlledFailureForInvalidDocument(string csv, string code)
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync(csv);
        var result = await new CsvImportSourceReader(file.Path).ReadAsync();
        Assert.Equal(ImportReadStatus.UnsupportedSource, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public async Task CsvIncompleteRowBecomesCandidateAndDoesNotDiscardValidRows()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("Title,Artist\n,No title\nGood,Artist\n");
        var result = await new CsvImportSourceReader(file.Path).ReadAsync();
        Assert.Equal(ImportReadStatus.Success, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Empty(result.Candidates[0].Title);
        Assert.Contains(result.Candidates[0].Diagnostics, d => d.Code == "row.incomplete");
        Assert.Equal("Good", result.Candidates[1].Title);
    }

    [Fact]
    public async Task CsvInvalidDateHasNullFoundAtAndWarning()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("Title,Artist,FoundAt\nSong,Artist,not-a-date\n");
        var item = Assert.Single((await new CsvImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Null(item.FoundAt);
        Assert.Contains(item.Diagnostics, d => d.Code == "date.invalid");
    }

    [Fact]
    public async Task CsvCalendarDateIsNotInventedAsTimestamp()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("Title,Artist,FoundAt\nSong,Artist,2026-02-03\n");
        var item = Assert.Single((await new CsvImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Null(item.FoundAt);
        Assert.Contains(item.Diagnostics, d => d.Code == "date.precision.unsupported");
    }

    [Fact]
    public async Task CsvUnknownSourceFallsBackWithWarningAndShazamIdDoesNotBecomeEventId()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("Title,Artist,Source,ShazamId\nSong,Artist,Other,track-key\n");
        var item = Assert.Single((await new CsvImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Equal(FindSource.ImportedUnknown, item.Source);
        Assert.Null(item.SourceItemId);
        Assert.Contains(item.Diagnostics, d => d.Code == "source.invalid");
        Assert.Contains(item.ExternalReferences, reference => reference.Provider == ExternalReferenceProvider.Shazam);
    }

    [Fact]
    public async Task CsvDoesNotDeduplicateRepeatedRowsAndReadsUtf8Bom()
    {
        using var file = new TempFile(".csv");
        await File.WriteAllTextAsync(file.Path, "Title,Artist\nSame,Artist\nSame,Artist\n", new UTF8Encoding(true));
        var result = await new CsvImportSourceReader(file.Path).ReadAsync();
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task CurrentSongStashCsvExportShapeIsReadableAsGenericMetadata()
    {
        using var file = new TempFile(".csv");
        await file.WriteAsync("SongId,Title,Artist,Album,ArtworkUrl,Genre,Notes,IsFavorite,FoundAt,Source,ImportedAt,SourceItemId,ShazamId,ShazamUrl,AppleMusicId,Isrc\n" +
            "id,Song,Artist,Album,https://art.test/a.png,ignored,ignored,true,2026-10-27T10:00:00Z,ShazamApple,2026-10-27T10:01:00Z,event-1,shazam-id,https://shazam.test/song,apple-id,ISRC\n");
        var item = Assert.Single((await new CsvImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Equal("Song", item.Title);
        Assert.Equal(FindSource.ShazamApple, item.Source);
        Assert.Equal("event-1", item.SourceItemId);
        Assert.Equal(3, item.ExternalReferences.Count);
        Assert.Null(item.Diagnostics.FirstOrDefault(d => d.Code == "source.invalid"));
    }

    [Fact]
    public async Task CsvMissingFileIsControlledUnavailable()
    {
        var result = await new CsvImportSourceReader(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv")).ReadAsync();
        Assert.Equal(ImportReadStatus.Unavailable, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Code == "file.not_found");
    }

    [Fact]
    public async Task JsonReadsArrayAndCaseInsensitiveProperties()
    {
        using var file = new TempFile(".json");
        await file.WriteAsync("[{\"TITLE\":\"Song\",\"Artist\":\"Artist\",\"Album\":\"Album\"}]");
        var result = await new JsonImportSourceReader(file.Path).ReadAsync();
        var item = Assert.Single(result.Candidates);
        Assert.Equal("Song", item.Title);
        Assert.Equal("Artist", item.Artist);
        Assert.Equal("Album", item.Album);
    }

    [Fact]
    public async Task JsonReadsSongsObjectAndSupportedFields()
    {
        using var file = new TempFile(".json");
        await file.WriteAsync("{\"songs\":[{\"title\":\"Song\",\"artist\":\"Artist\",\"foundAt\":\"2026-01-01T00:00:00Z\",\"sourceItemId\":\"event\",\"shazamId\":\"key\",\"appleMusicId\":\"music\"}]}");
        var item = Assert.Single((await new JsonImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), item.FoundAt);
        Assert.Equal("event", item.SourceItemId);
        Assert.Equal(2, item.ExternalReferences.Count);
    }

    [Fact]
    public async Task JsonMissingSourceUsesImportedUnknownAndValidSourceIsParsed()
    {
        using var file = new TempFile(".json");
        await file.WriteAsync("[{\"title\":\"A\",\"artist\":\"X\"},{\"title\":\"B\",\"artist\":\"Y\",\"source\":\"Manual\"}]");
        var items = (await new JsonImportSourceReader(file.Path).ReadAsync()).Candidates;
        Assert.Equal(FindSource.ImportedUnknown, items[0].Source);
        Assert.Empty(items[0].Diagnostics);
        Assert.Equal(FindSource.Manual, items[1].Source);
    }

    [Fact]
    public async Task JsonUnsupportedSourceGetsFallbackWarning()
    {
        using var file = new TempFile(".json");
        await file.WriteAsync("[{\"title\":\"Song\",\"artist\":\"Artist\",\"source\":\"bogus\"}]");
        var item = Assert.Single((await new JsonImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Equal(FindSource.ImportedUnknown, item.Source);
        Assert.Contains(item.Diagnostics, diagnostic => diagnostic.Code == "source.invalid");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"albums\":[]}")]
    [InlineData("42")]
    [InlineData("{\"songs\":{}}")]
    public async Task JsonMalformedOrUnsupportedShapeReturnsControlledResult(string json)
    {
        using var file = new TempFile(".json");
        await file.WriteAsync(json);
        var result = await new JsonImportSourceReader(file.Path).ReadAsync();
        Assert.Equal(ImportReadStatus.UnsupportedSource, result.Status);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public async Task EmptyJsonArrayIsSuccessfulEmptyImport()
    {
        using var file = new TempFile(".json");
        await file.WriteAsync("[]");
        var result = await new JsonImportSourceReader(file.Path).ReadAsync();
        Assert.Equal(ImportReadStatus.Success, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task JsonInvalidRowDoesNotDestroyValidRowsAndDuplicatesRemainSeparate()
    {
        using var file = new TempFile(".json");
        await file.WriteAsync("[{\"title\":\"Same\",\"artist\":\"Artist\"},null,{\"title\":\"Same\",\"artist\":\"Artist\"}]");
        var result = await new JsonImportSourceReader(file.Path).ReadAsync();
        Assert.Equal(ImportReadStatus.Success, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Contains(result.Diagnostics, d => d.Code == "json.row.invalid");
    }

    [Fact]
    public async Task SongStashExportJsonImportsTopLevelMetadataButDoesNotRestoreNestedRecords()
    {
        using var file = new TempFile(".json");
        await file.WriteAsync("{\"format\":\"SongStash.LibraryExport\",\"schemaVersion\":1,\"songs\":[{" +
            "\"title\":\"Song\",\"artist\":\"Artist\",\"album\":\"Album\",\"findEvents\":[{\"source\":\"Manual\",\"foundAt\":\"2026-01-01T00:00:00Z\"}]," +
            "\"externalReferences\":[{\"provider\":\"Shazam\",\"externalId\":\"track-key\"}]}]}");
        var item = Assert.Single((await new JsonImportSourceReader(file.Path).ReadAsync()).Candidates);
        Assert.Equal("Song", item.Title);
        Assert.Equal("Album", item.Album);
        Assert.Equal(FindSource.ImportedUnknown, item.Source);
        Assert.Null(item.FoundAt);
        Assert.Empty(item.ExternalReferences);
    }

    [Fact]
    public void FactorySelectsReaderByExtensionAndRejectsUnsupportedExtension()
    {
        var factory = new FileImportSourceReaderFactory();
        Assert.IsType<CsvImportSourceReader>(factory.Create("list.CSV"));
        Assert.IsType<JsonImportSourceReader>(factory.Create("list.json"));
        Assert.Throws<ArgumentException>(() => factory.Create("list.xml"));
    }

    private sealed class TempFile(string extension) : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + extension);
        public Task WriteAsync(string content) => File.WriteAllTextAsync(Path, content, new UTF8Encoding(false));
        public void Dispose() { if (File.Exists(Path)) File.Delete(Path); }
    }
}
