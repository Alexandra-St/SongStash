using SongStash.Application.Matching;

namespace SongStash.Application.LibraryActions;

public sealed record ManualAddResult(
    ManualAddOutcome Outcome,
    Guid? SongId = null,
    SongMatchResult? Match = null,
    string? Message = null);
