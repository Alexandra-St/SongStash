using SongStash.Domain.ExternalReferences;

namespace SongStash.Application.Persistence;

public interface IExternalReferenceRepository
{
    Task<IReadOnlyList<ExternalReference>> GetBySongIdsAsync(
        IReadOnlyCollection<Guid> songIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(ExternalReference reference, CancellationToken cancellationToken = default);
}
