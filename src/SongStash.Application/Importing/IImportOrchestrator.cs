namespace SongStash.Application.Importing;

public interface IImportOrchestrator
{
    Task<ImportPreviewRunResult> ReadPreviewAsync(CancellationToken cancellationToken = default);
    Task<ImportPreviewRunResult> ReadPreviewAsync(
        IImportSourceReader sourceReader,
        CancellationToken cancellationToken = default) => ReadPreviewAsync(cancellationToken);
    Task<ImportResult> ImportAsync(
        IEnumerable<ImportPreviewItem> confirmedItems,
        CancellationToken cancellationToken = default);
}
