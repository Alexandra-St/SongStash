using SongStash.Domain.Finds;

namespace SongStash.Application.Importing;

/// <summary>A normalized source record before matching or import.</summary>
public sealed record ImportCandidate
{
    public ImportCandidate(string title, string artist, FindSource source)
    {
        Title = title;
        Artist = artist;
        Source = source;
    }

    public string Title { get; init; }
    public string Artist { get; init; }
    public FindSource Source { get; init; }
    public string? Album { get; init; }
    public Uri? ArtworkUrl { get; init; }
    public DateTimeOffset? FoundAt { get; init; }
    public string? SourceItemId { get; init; }
    public IReadOnlyList<ExternalReferenceCandidate> ExternalReferences { get; init; } = [];
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];
}
