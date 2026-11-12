using SongStash.Domain.ExternalReferences;

namespace SongStash.Application.Importing;

public sealed record ExternalReferenceCandidate(
    ExternalReferenceProvider Provider,
    string Value,
    Uri? Url = null);
