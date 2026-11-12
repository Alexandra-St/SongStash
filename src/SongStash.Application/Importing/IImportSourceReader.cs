namespace SongStash.Application.Importing;

/// <summary>
/// Reads and normalizes records from one external source. Implementations do not match,
/// deduplicate, or persist candidates.
/// </summary>
public interface IImportSourceReader
{
    Task<ImportReadResult> ReadAsync(CancellationToken cancellationToken = default);
}
