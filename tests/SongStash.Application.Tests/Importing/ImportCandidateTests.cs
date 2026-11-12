using SongStash.Domain.ExternalReferences;
using Xunit;
using SongStash.Application.Importing;
using SongStash.Domain.Finds;

namespace SongStash.Application.Tests.Importing;

public sealed class ImportCandidateTests
{
    [Fact]
    public void OptionalMetadataCanBeNull()
    {
        var candidate = new ImportCandidate("Title", "Artist", FindSource.ShazamApple);

        Assert.Null(candidate.Album);
        Assert.Null(candidate.ArtworkUrl);
        Assert.Null(candidate.FoundAt);
        Assert.Null(candidate.SourceItemId);
        Assert.Empty(candidate.ExternalReferences);
        Assert.Empty(candidate.Diagnostics);
    }

    [Fact]
    public void CandidateCanContainMultipleExternalReferences()
    {
        var references = new[]
        {
            new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "key"),
            new ExternalReferenceCandidate(ExternalReferenceProvider.AppleMusic, "123"),
            new ExternalReferenceCandidate(ExternalReferenceProvider.Isrc, "USAAA0000001")
        };

        var candidate = new ImportCandidate("Title", "Artist", FindSource.ShazamApple)
        {
            ExternalReferences = references
        };

        Assert.Equal(3, candidate.ExternalReferences.Count);
    }

    [Fact]
    public void CandidateCanContainMultipleDiagnostics()
    {
        var diagnostics = new[]
        {
            new ImportDiagnostic(ImportDiagnosticSeverity.Warning, "missing-album", "Album is unavailable."),
            new ImportDiagnostic(ImportDiagnosticSeverity.Info, "source-row", "Read from source row.")
        };

        var candidate = new ImportCandidate("Title", "Artist", FindSource.ShazamApple)
        {
            Diagnostics = diagnostics
        };

        Assert.Equal(2, candidate.Diagnostics.Count);
    }
}
