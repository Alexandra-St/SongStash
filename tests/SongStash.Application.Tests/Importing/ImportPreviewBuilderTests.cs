using SongStash.Application.Importing;
using SongStash.Application.Matching;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using Xunit;

namespace SongStash.Application.Tests.Importing;

public sealed class ImportPreviewBuilderTests
{
    private readonly ImportPreviewBuilder _builder = new(new SongMatcher());

    [Fact]
    public void CompleteCandidateWithoutMatchIsNew()
    {
        var item = Assert.Single(_builder.Build([Candidate("New", "Artist")], [], []));

        Assert.Equal(ImportPreviewStatus.New, item.Status);
        Assert.Null(item.MatchedSongId);
        Assert.Empty(item.PossibleSongIds);
    }

    [Fact]
    public void ExactExternalIdMatchIsAlreadyInLibrary()
    {
        var song = new Song("Stored", "Artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Isrc, "USAAA0000001");
        var candidate = Candidate("Different", "Artist",
            new ExternalReferenceCandidate(ExternalReferenceProvider.Isrc, "USAAA0000001"));

        var item = Assert.Single(_builder.Build([candidate], [song], [reference]));

        Assert.Equal(ImportPreviewStatus.AlreadyInLibrary, item.Status);
        Assert.Equal(song.Id, item.MatchedSongId);
        Assert.Equal(SongMatchReason.ExternalId, item.MatchReason);
    }

    [Fact]
    public void ExactArtistAndTitleMatchIsAlreadyInLibrary()
    {
        var song = new Song("Title", "Artist");

        var item = Assert.Single(_builder.Build([Candidate("Title", "Artist")], [song], []));

        Assert.Equal(ImportPreviewStatus.AlreadyInLibrary, item.Status);
        Assert.Equal(song.Id, item.MatchedSongId);
        Assert.Equal(SongMatchReason.ArtistAndTitle, item.MatchReason);
    }

    [Fact]
    public void AmbiguousMatcherResultRequiresReview()
    {
        var first = new Song("Title", "Artist");
        var second = new Song("Title", "Artist");

        var item = Assert.Single(_builder.Build([Candidate("Title", "Artist")], [first, second], []));

        Assert.Equal(ImportPreviewStatus.NeedsReview, item.Status);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), item.PossibleSongIds.Order());
        Assert.Equal(SongMatchReason.Ambiguous, item.MatchReason);
    }

    [Fact]
    public void MissingOrWhitespaceTitleIsIncomplete()
    {
        Assert.Equal(ImportPreviewStatus.Incomplete,
            Assert.Single(_builder.Build([Candidate("", "Artist")], [], [])).Status);
        Assert.Equal(ImportPreviewStatus.Incomplete,
            Assert.Single(_builder.Build([Candidate("  ", "Artist")], [], [])).Status);
    }

    [Fact]
    public void MissingOrWhitespaceArtistIsIncomplete()
    {
        Assert.Equal(ImportPreviewStatus.Incomplete,
            Assert.Single(_builder.Build([Candidate("Title", "")], [], [])).Status);
        Assert.Equal(ImportPreviewStatus.Incomplete,
            Assert.Single(_builder.Build([Candidate("Title", " \t")], [], [])).Status);
    }

    [Fact]
    public void IncompleteMetadataTakesPriorityOverExternalIdMatch()
    {
        var song = new Song("Title", "Artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "same-key");
        var incomplete = Candidate(" ", "Artist",
            new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "same-key"));

        var item = Assert.Single(_builder.Build([incomplete], [song], [reference]));

        Assert.Equal(ImportPreviewStatus.Incomplete, item.Status);
        Assert.Null(item.MatchedSongId);
        Assert.Empty(item.PossibleSongIds);
    }

    [Fact]
    public void ReviewItemContainsEveryPossibleSongId()
    {
        var songs = new[] { new Song("Title", "Artist"), new Song("Title", "Artist"), new Song("Title", "Artist") };

        var item = Assert.Single(_builder.Build([Candidate("Title", "Artist")], songs, []));

        Assert.Equal(ImportPreviewStatus.NeedsReview, item.Status);
        Assert.Equal(songs.Select(song => song.Id).Order(), item.PossibleSongIds.Order());
    }

    [Fact]
    public void CandidateDiagnosticsArePreserved()
    {
        var diagnostics = new[]
        {
            new ImportDiagnostic(ImportDiagnosticSeverity.Warning, "source-warning", "A warning.")
        };
        var candidate = Candidate("Title", "Artist") with { Diagnostics = diagnostics };

        var item = Assert.Single(_builder.Build([candidate], [], []));

        Assert.Same(diagnostics, item.Diagnostics);
    }

    [Fact]
    public void WarningAloneDoesNotChangeNewStatus()
    {
        var warning = new ImportDiagnostic(ImportDiagnosticSeverity.Warning, "optional-metadata", "Optional value missing.");
        var candidate = Candidate("Title", "Artist") with { Diagnostics = [warning] };

        var item = Assert.Single(_builder.Build([candidate], [], []));

        Assert.Equal(ImportPreviewStatus.New, item.Status);
        Assert.Single(item.Diagnostics);
    }

    [Fact]
    public void PreviewItemsPreserveCandidateOrder()
    {
        var candidates = new[]
        {
            Candidate("New", "Artist"),
            Candidate("", "Artist"),
            Candidate("Matched", "Artist")
        };
        var matchedSong = new Song("Matched", "Artist");

        var items = _builder.Build(candidates, [matchedSong], []);

        Assert.Equal(candidates, items.Select(item => item.Candidate));
        Assert.Equal(
            [ImportPreviewStatus.New, ImportPreviewStatus.Incomplete, ImportPreviewStatus.AlreadyInLibrary],
            items.Select(item => item.Status));
    }

    [Fact]
    public void RepeatRecognitionCandidatesRemainSeparatePreviewItems()
    {
        var song = new Song("Same song", "Artist");
        var trackReference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "track-key");
        var first = Candidate("Same song", "Artist", new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key"))
            with { SourceItemId = "event-one", FoundAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
        var second = Candidate("Same song", "Artist", new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key"))
            with { SourceItemId = "event-two", FoundAt = DateTimeOffset.UtcNow };

        var items = _builder.Build([first, second], [song], [trackReference]);

        Assert.Equal(2, items.Count);
        Assert.Same(first, items[0].Candidate);
        Assert.Same(second, items[1].Candidate);
        Assert.All(items, item => Assert.Equal(ImportPreviewStatus.AlreadyInLibrary, item.Status));
        Assert.All(items, item => Assert.Equal(song.Id, item.MatchedSongId));
    }

    [Fact]
    public void SourceItemIdDoesNotInfluencePreviewMatching()
    {
        var song = new Song("Existing song", "Artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "event-id");
        var candidate = Candidate("Different song", "Artist") with { SourceItemId = "event-id" };

        var item = Assert.Single(_builder.Build([candidate], [song], [reference]));

        Assert.Equal(ImportPreviewStatus.New, item.Status);
    }

    [Fact]
    public void AlreadyInLibraryDoesNotCollapseRecognitionEvents()
    {
        var song = new Song("Same song", "Artist");
        var reference = new ExternalReference(song.Id, ExternalReferenceProvider.Shazam, "track-key");
        var candidates = new[]
        {
            Candidate("Same song", "Artist", new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key")) with { SourceItemId = "one" },
            Candidate("Same song", "Artist", new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key")) with { SourceItemId = "two" }
        };

        var items = _builder.Build(candidates, [song], [reference]);

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(ImportPreviewStatus.AlreadyInLibrary, item.Status));
        Assert.Equal(new[] { "one", "two" }, items.Select(item => item.Candidate.SourceItemId));
    }

    [Fact]
    public void EmptyCandidatesReturnEmptyPreview()
    {
        Assert.Empty(_builder.Build([], [], []));
    }

    private static ImportCandidate Candidate(
        string title,
        string artist,
        params ExternalReferenceCandidate[] references) =>
        new(title, artist, FindSource.ImportedUnknown)
        {
            ExternalReferences = references
        };
}
