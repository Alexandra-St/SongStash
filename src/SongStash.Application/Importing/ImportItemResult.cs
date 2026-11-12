namespace SongStash.Application.Importing;

public sealed record ImportItemResult(
    ImportPreviewItem PreviewItem,
    ImportItemOutcomeStatus Status,
    string? Message = null);
