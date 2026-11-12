using SongStash.Application.Matching;

namespace SongStash.Application.Importing;

public sealed record ImportPreviewItem(
    ImportCandidate Candidate,
    ImportPreviewStatus Status,
    Guid? MatchedSongId,
    IReadOnlyList<Guid> PossibleSongIds,
    SongMatchReason? MatchReason,
    IReadOnlyList<ImportDiagnostic> Diagnostics);
