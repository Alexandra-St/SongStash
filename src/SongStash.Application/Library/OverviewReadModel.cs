namespace SongStash.Application.Library;

public sealed record OverviewReadModel(
    int TotalSongs,
    int TotalFinds,
    int FavoritesCount,
    int ArtistsCount,
    int GenresCount,
    IReadOnlyList<OverviewSongItem> RecentFinds,
    IReadOnlyList<OverviewSongItem> Favorites,
    IReadOnlyList<OverviewArtistItem> TopArtists,
    IReadOnlyList<OverviewGenreItem> TopGenres,
    IReadOnlyList<OverviewAlbumItem> RecentAlbums);

public sealed record OverviewSongItem(
    Guid SongId,
    Guid? FindEventId,
    string Title,
    string Artist,
    string? Album,
    Uri? ArtworkUrl,
    DateTimeOffset? FoundAt);

public sealed record OverviewArtistItem(string Name, int SongCount);
public sealed record OverviewGenreItem(string Name, int SongCount);
public sealed record OverviewAlbumItem(string Name, string Artist, string SongTitle, Guid SongId,
    Uri? ArtworkUrl, DateTimeOffset LatestFoundAt);

public interface IOverviewQueryService
{
    Task<OverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default);
}
