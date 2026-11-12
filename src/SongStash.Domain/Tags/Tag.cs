namespace SongStash.Domain.Tags;

public sealed class Tag
{
    public Tag(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name cannot be null, empty, or whitespace.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name;
        NormalizedName = name.Trim().ToUpperInvariant();
    }

    public Guid Id { get; }
    public string Name { get; }
    public string NormalizedName { get; }
}
