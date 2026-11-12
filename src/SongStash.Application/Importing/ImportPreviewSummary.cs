namespace SongStash.Application.Importing;

public sealed record ImportPreviewSummary(
    int TotalCandidates,
    int NewSongs,
    int AlreadyInLibrary,
    int Incomplete,
    int NeedsReview)
{
    public static ImportPreviewSummary From(IReadOnlyCollection<ImportPreviewItem> items) => new(
        items.Count,
        items.Count(item => item.Status == ImportPreviewStatus.New),
        items.Count(item => item.Status == ImportPreviewStatus.AlreadyInLibrary),
        items.Count(item => item.Status == ImportPreviewStatus.Incomplete),
        items.Count(item => item.Status == ImportPreviewStatus.NeedsReview));
}
