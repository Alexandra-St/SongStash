namespace SongStash.Application.Importing;

public enum ImportItemOutcomeStatus
{
    AddedSong,
    AddedFindEvent,
    SkippedDuplicate,
    NeedsAttention,
    Failed
}
