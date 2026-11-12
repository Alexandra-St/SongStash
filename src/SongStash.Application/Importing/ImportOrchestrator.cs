using SongStash.Application.Matching;
using SongStash.Application.Persistence;

namespace SongStash.Application.Importing;

/// <summary>Coordinates the existing reader, preview, and import use cases without owning their business rules.</summary>
public sealed class ImportOrchestrator(
    IImportSourceReader sourceReader,
    ISongRepository songs,
    IExternalReferenceRepository externalReferences,
    ImportPreviewBuilder previewBuilder,
    ImportService importService) : IImportOrchestrator
{
    public Task<ImportPreviewRunResult> ReadPreviewAsync(CancellationToken cancellationToken = default) =>
        ReadPreviewAsync(sourceReader, cancellationToken);

    public async Task<ImportPreviewRunResult> ReadPreviewAsync(
        IImportSourceReader selectedSourceReader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedSourceReader);
        ImportReadResult readResult;
        try
        {
            readResult = await selectedSourceReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return ImportPreviewRunResult.Failure(ImportPreviewRunStatus.Failed,
            [new ImportDiagnostic(ImportDiagnosticSeverity.Error, "import.read.failed",
                "The selected import source could not be read.")]);
        }

        if (readResult.Status == ImportReadStatus.Unavailable)
        {
            return ImportPreviewRunResult.Failure(ImportPreviewRunStatus.SourceUnavailable, readResult.Diagnostics);
        }

        if (readResult.Status == ImportReadStatus.UnsupportedSource)
        {
            return ImportPreviewRunResult.Failure(ImportPreviewRunStatus.UnsupportedSchema, readResult.Diagnostics);
        }

        try
        {
            var existingSongs = await songs.GetAllForMatchingAsync(cancellationToken).ConfigureAwait(false);
            var references = existingSongs.Count == 0
                ? []
                : await externalReferences.GetBySongIdsAsync(
                    existingSongs.Select(song => song.Id).ToArray(), cancellationToken).ConfigureAwait(false);
            var items = previewBuilder.Build(readResult.Candidates, existingSongs, references);
            return ImportPreviewRunResult.Success(items, readResult.Diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return ImportPreviewRunResult.Failure(ImportPreviewRunStatus.Failed,
            [.. readResult.Diagnostics,
                new ImportDiagnostic(ImportDiagnosticSeverity.Error, "import.preview.failed",
                    "The import preview could not be prepared.")]);
        }
    }

    public Task<ImportResult> ImportAsync(
        IEnumerable<ImportPreviewItem> confirmedItems,
        CancellationToken cancellationToken = default) =>
        importService.ImportAsync(confirmedItems, cancellationToken);
}
