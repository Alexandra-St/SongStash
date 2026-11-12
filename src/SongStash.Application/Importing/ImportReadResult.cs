namespace SongStash.Application.Importing;

/// <summary>The outcome of reading a source, including non-fatal diagnostics.</summary>
public sealed record ImportReadResult(
    ImportReadStatus Status,
    IReadOnlyList<ImportCandidate> Candidates,
    IReadOnlyList<ImportDiagnostic> Diagnostics)
{
    public static ImportReadResult Success(
        IEnumerable<ImportCandidate>? candidates = null,
        IEnumerable<ImportDiagnostic>? diagnostics = null) =>
        new(
            ImportReadStatus.Success,
            candidates?.ToArray() ?? [],
            diagnostics?.ToArray() ?? []);

    public static ImportReadResult Unavailable(ImportDiagnostic diagnostic) =>
        new(ImportReadStatus.Unavailable, [], [diagnostic]);

    public static ImportReadResult UnsupportedSource(ImportDiagnostic diagnostic) =>
        new(ImportReadStatus.UnsupportedSource, [], [diagnostic]);
}
