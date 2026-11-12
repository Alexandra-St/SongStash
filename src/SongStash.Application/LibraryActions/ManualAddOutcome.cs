namespace SongStash.Application.LibraryActions;

public enum ManualAddOutcome
{
    AddedNewSong,
    AddedFindToExistingSong,
    ExactMatchRequiresChoice,
    NeedsReview,
    Invalid
}
