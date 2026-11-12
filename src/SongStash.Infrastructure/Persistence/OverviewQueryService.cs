using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SongStash.Application.Library;

namespace SongStash.Infrastructure.Persistence;

/// <summary>Projects Overview summaries with bounded, set-based database queries.</summary>
public sealed class OverviewQueryService(SongStashDbContext database) : IOverviewQueryService
{
    private const int RecentLimit = 5;
    private const int RankingLimit = 5;
    private const int AlbumLimit = 6;

    public async Task<OverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var songs = database.Songs.AsNoTracking();
        var totalSongs = await songs.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalFinds = await database.FindEvents.CountAsync(cancellationToken).ConfigureAwait(false);
        var favoritesCount = await songs.CountAsync(song => song.IsFavorite, cancellationToken).ConfigureAwait(false);
        var artistsCount = await songs.Select(song => song.Artist).Distinct().CountAsync(cancellationToken).ConfigureAwait(false);
        var genresCount = await songs.Where(song => song.Genre != null && song.Genre.Trim() != "")
            .Select(song => song.Genre!).Distinct().CountAsync(cancellationToken).ConfigureAwait(false);

        var recentFinds = await ReadRecentFindsAsync(cancellationToken).ConfigureAwait(false);

        var favorites = await songs.Where(song => song.IsFavorite)
            .OrderBy(song => song.Title).ThenBy(song => song.Artist)
            .Take(RecentLimit)
            .Select(song => new OverviewSongItem(song.Id, null, song.Title, song.Artist, song.Album, song.ArtworkUrl, null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var topArtistRows = await songs.GroupBy(song => song.Artist)
            .Select(group => new { Name = group.Key, SongCount = group.Count() })
            .OrderByDescending(item => item.SongCount).ThenBy(item => item.Name)
            .Take(RankingLimit).ToListAsync(cancellationToken).ConfigureAwait(false);
        var topArtists = topArtistRows.Select(item => new OverviewArtistItem(item.Name, item.SongCount)).ToArray();

        var topGenreRows = await songs.Where(song => song.Genre != null && song.Genre.Trim() != "")
            .GroupBy(song => song.Genre!)
            .Select(group => new { Name = group.Key, SongCount = group.Count() })
            .OrderByDescending(item => item.SongCount).ThenBy(item => item.Name)
            .Take(RankingLimit).ToListAsync(cancellationToken).ConfigureAwait(false);
        var topGenres = topGenreRows.Select(item => new OverviewGenreItem(item.Name, item.SongCount)).ToArray();

        var recentAlbums = await ReadRecentAlbumsAsync(cancellationToken).ConfigureAwait(false);
        return new OverviewReadModel(totalSongs, totalFinds, favoritesCount, artistsCount, genresCount,
            recentFinds, favorites, topArtists, topGenres, recentAlbums);
    }

    private async Task<IReadOnlyList<OverviewSongItem>> ReadRecentFindsAsync(CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT s.Id, f.Id, s.Title, s.Artist, s.Album, s.ArtworkUrl, f.FoundAt
                FROM FindEvents f JOIN Songs s ON s.Id = f.SongId
                WHERE f.FoundAt IS NOT NULL
                ORDER BY julianday(f.FoundAt) DESC, f.Id ASC
                LIMIT $limit;
                """;
            var limit = command.CreateParameter(); limit.ParameterName = "$limit"; limit.Value = RecentLimit;
            command.Parameters.Add(limit);
            var rows = new List<OverviewSongItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new OverviewSongItem(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                    reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : new Uri(reader.GetString(5), UriKind.RelativeOrAbsolute),
                    DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture)));
            }
            return rows;
        }
        finally { await database.Database.CloseConnectionAsync().ConfigureAwait(false); }
    }

    private async Task<IReadOnlyList<OverviewAlbumItem>> ReadRecentAlbumsAsync(CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT s.Album AS AlbumName,
                       s.Artist AS ArtistName,
                       (SELECT f2.FoundAt FROM Songs s2
                        JOIN FindEvents f2 ON f2.SongId = s2.Id
                        WHERE s2.Album = s.Album AND s2.Artist = s.Artist AND f2.FoundAt IS NOT NULL
                        ORDER BY julianday(f2.FoundAt) DESC, f2.Id ASC LIMIT 1) AS LatestFoundAt,
                       (SELECT s2.Id FROM Songs s2
                        JOIN FindEvents f2 ON f2.SongId = s2.Id
                        WHERE s2.Album = s.Album AND s2.Artist = s.Artist AND f2.FoundAt IS NOT NULL
                        ORDER BY julianday(f2.FoundAt) DESC, f2.Id ASC LIMIT 1) AS SongId,
                       (SELECT s2.Title FROM Songs s2
                        JOIN FindEvents f2 ON f2.SongId = s2.Id
                        WHERE s2.Album = s.Album AND s2.Artist = s.Artist AND f2.FoundAt IS NOT NULL
                        ORDER BY julianday(f2.FoundAt) DESC, f2.Id ASC LIMIT 1) AS SongTitle,
                       (SELECT s2.ArtworkUrl FROM Songs s2
                        JOIN FindEvents f2 ON f2.SongId = s2.Id
                        WHERE s2.Album = s.Album AND s2.Artist = s.Artist AND f2.FoundAt IS NOT NULL
                        ORDER BY julianday(f2.FoundAt) DESC, f2.Id ASC LIMIT 1) AS ArtworkUrl
                FROM Songs s
                JOIN FindEvents f ON f.SongId = s.Id
                WHERE s.Album IS NOT NULL AND TRIM(s.Album) <> '' AND f.FoundAt IS NOT NULL
                GROUP BY s.Artist, s.Album
                ORDER BY MAX(julianday(f.FoundAt)) DESC, s.Artist ASC, s.Album ASC
                LIMIT $limit;
                """;
            var limit = command.CreateParameter();
            limit.ParameterName = "$limit";
            limit.Value = AlbumLimit;
            command.Parameters.Add(limit);
            var rows = new List<OverviewAlbumItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new OverviewAlbumItem(
                    reader.GetString(0), reader.GetString(1), reader.GetString(4), Guid.Parse(reader.GetString(3)),
                    reader.IsDBNull(5) ? null : new Uri(reader.GetString(5), UriKind.RelativeOrAbsolute),
                    DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
            }
            return rows;
        }
        finally { await database.Database.CloseConnectionAsync().ConfigureAwait(false); }
    }
}
