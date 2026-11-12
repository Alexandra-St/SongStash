namespace SongStash.Domain.Songs;

public sealed class Song
{
    public Song(
        string title,
        string artist,
        string? album = null,
        Uri? artworkUrl = null,
        string? genre = null,
        string? notes = null)
        : this(title, artist, album, artworkUrl, genre, notes, DateTimeOffset.UtcNow)
    {
    }

    public Song(
        string title,
        string artist,
        string? album,
        Uri? artworkUrl,
        string? genre,
        string? notes,
        DateTimeOffset createdAt)
    {
        ValidateRequired(title, nameof(title));
        ValidateRequired(artist, nameof(artist));

        Id = Guid.NewGuid();
        Title = title;
        Artist = artist;
        Album = album;
        ArtworkUrl = artworkUrl;
        Genre = genre;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; }
    public string Title { get; private set; }
    public string Artist { get; private set; }
    public string? Album { get; private set; }
    public Uri? ArtworkUrl { get; private set; }
    public string? Genre { get; private set; }
    public string? Notes { get; private set; }
    public bool IsFavorite { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateDetails(
        string title,
        string artist,
        string? album,
        Uri? artworkUrl,
        string? genre,
        string? notes)
    {
        ValidateRequired(title, nameof(title));
        ValidateRequired(artist, nameof(artist));

        Title = title;
        Artist = artist;
        Album = album;
        ArtworkUrl = artworkUrl;
        Genre = genre;
        Notes = notes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetFavorite(bool isFavorite)
    {
        if (IsFavorite == isFavorite)
        {
            return;
        }

        IsFavorite = isFavorite;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", parameterName);
        }
    }
}
