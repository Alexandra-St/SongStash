namespace SongStash.Application.Importing;

public sealed record ImportPreviewRunResult(
    ImportPreviewRunStatus Status,
    IReadOnlyList<ImportPreviewItem> Items,
    IReadOnlyList<ImportDiagnostic> Diagnostics,
    ImportPreviewSummary? Summary)
{
    public static ImportPreviewRunResult Success(
        IReadOnlyList<ImportPreviewItem> items,
        IReadOnlyList<ImportDiagnostic> diagnostics) =>
        new(ImportPreviewRunStatus.Success, items, diagnostics, ImportPreviewSummary.From(items));

    public static ImportPreviewRunResult Failure(
        ImportPreviewRunStatus status,
        IReadOnlyList<ImportDiagnostic> diagnostics) => new(status, [], diagnostics, null);
}
