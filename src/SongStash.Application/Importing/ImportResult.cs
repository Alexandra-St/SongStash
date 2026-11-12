namespace SongStash.Application.Importing;

public sealed record ImportResult(IReadOnlyList<ImportItemResult> Items)
{
    public int AddedSongs => Items.Count(item => item.Status == ImportItemOutcomeStatus.AddedSong);

    public int AddedFindEvents => Items.Count(item =>
        item.Status is ImportItemOutcomeStatus.AddedSong or ImportItemOutcomeStatus.AddedFindEvent);

    public int SkippedDuplicates => Items.Count(item => item.Status == ImportItemOutcomeStatus.SkippedDuplicate);

    public int NeedsAttention => Items.Count(item => item.Status == ImportItemOutcomeStatus.NeedsAttention);

    public int Failed => Items.Count(item => item.Status == ImportItemOutcomeStatus.Failed);
}
