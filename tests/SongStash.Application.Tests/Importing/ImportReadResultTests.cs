using Xunit;
using SongStash.Application.Importing;
using SongStash.Domain.Finds;

namespace SongStash.Application.Tests.Importing;

public sealed class ImportReadResultTests
{
    [Fact]
    public void ResultDistinguishesSuccessUnavailableAndUnsupportedSource()
    {
        var success = ImportReadResult.Success();
        var unavailable = ImportReadResult.Unavailable(
            new ImportDiagnostic(ImportDiagnosticSeverity.Error, "source-unavailable", "Source is unavailable."));
        var unsupported = ImportReadResult.UnsupportedSource(
            new ImportDiagnostic(ImportDiagnosticSeverity.Error, "unsupported-schema", "Source schema is unsupported."));

        Assert.Equal(ImportReadStatus.Success, success.Status);
        Assert.Equal(ImportReadStatus.Unavailable, unavailable.Status);
        Assert.Equal(ImportReadStatus.UnsupportedSource, unsupported.Status);
    }

    [Fact]
    public void WarningDoesNotTurnSuccessfulReadIntoFailure()
    {
        var warning = new ImportDiagnostic(
            ImportDiagnosticSeverity.Warning,
            "incomplete-row",
            "A source row is incomplete.");
        var candidate = new ImportCandidate("Title", "Artist", FindSource.ImportedUnknown);

        var result = ImportReadResult.Success([candidate], [warning]);

        Assert.Equal(ImportReadStatus.Success, result.Status);
        Assert.Single(result.Candidates);
        Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticSeverity.Warning, result.Diagnostics[0].Severity);
    }
}
