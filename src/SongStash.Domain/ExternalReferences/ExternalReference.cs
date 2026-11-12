namespace SongStash.Domain.ExternalReferences;

public sealed class ExternalReference
{
    public ExternalReference(
        Guid songId,
        ExternalReferenceProvider provider,
        string? externalId = null,
        Uri? url = null)
    {
        if (string.IsNullOrWhiteSpace(externalId) && url is null)
        {
            throw new ArgumentException("An external ID or URL is required.", nameof(externalId));
        }

        Id = Guid.NewGuid();
        SongId = songId;
        Provider = provider;
        ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId;
        Url = url;
    }

    public Guid Id { get; }
    public Guid SongId { get; }
    public ExternalReferenceProvider Provider { get; }
    public string? ExternalId { get; }
    public Uri? Url { get; }
}
