namespace SongStash.Domain.Finds;

public sealed class FindEvent
{
    public FindEvent(
        Guid songId,
        FindSource source,
        DateTimeOffset importedAt,
        DateTimeOffset? foundAt = null,
        string? sourceItemId = null)
    {
        Id = Guid.NewGuid();
        SongId = songId;
        Source = source;
        ImportedAt = importedAt;
        FoundAt = foundAt;
        SourceItemId = sourceItemId;
    }

    public Guid Id { get; }
    public Guid SongId { get; }
    public DateTimeOffset? FoundAt { get; }
    public FindSource Source { get; }
    public DateTimeOffset ImportedAt { get; }
    public string? SourceItemId { get; }
}
