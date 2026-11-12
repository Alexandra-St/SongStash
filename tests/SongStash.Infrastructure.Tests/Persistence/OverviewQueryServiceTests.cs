using Microsoft.EntityFrameworkCore;
using SongStash.Application.Library;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Infrastructure.Persistence;
using Xunit;

namespace SongStash.Infrastructure.Tests.Persistence;

public sealed class OverviewQueryServiceTests
{
    [Fact]
    public async Task OverviewAggregatesSongsFindsFavoritesArtistsGenresAndRecentItemsSeparately()
    {
        await using var db = await TemporaryDatabase.CreateAsync();
        var betaOne = new Song("Beta One", "Beta", "Shared Album", genre: "Pop", notes: "note"); betaOne.SetFavorite(true);
        var betaTwo = new Song("Beta Two", "Beta", "Shared Album", genre: "Pop");
        var alphaOne = new Song("Alpha One", "Alpha", "Older Album", genre: "Rock");
        var alphaTwo = new Song("Alpha Two", "Alpha", album: "Shared Album", genre: " ");
        var betaOneOld = new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.FromHours(2)); // 11:00 UTC
        var betaTwoNew = new DateTimeOffset(2026, 10, 20, 11, 30, 0, TimeSpan.Zero);
        await using (var context = db.Context())
        {
            context.Songs.AddRange(betaOne, betaTwo, alphaOne, alphaTwo);
            context.FindEvents.AddRange(
                new FindEvent(betaOne.Id, FindSource.ShazamApple, betaOneOld, betaOneOld, "repeat-a"),
                new FindEvent(betaOne.Id, FindSource.ShazamApple, betaOneOld, betaOneOld.AddHours(-1), "repeat-b"),
                new FindEvent(betaTwo.Id, FindSource.ShazamApple, betaTwoNew, betaTwoNew, "beta-two"),
                new FindEvent(alphaOne.Id, FindSource.Manual, betaTwoNew, betaTwoNew.AddDays(-1), "alpha-one"),
                new FindEvent(alphaTwo.Id, FindSource.Manual, betaTwoNew, null, "unknown-date"));
            await context.SaveChangesAsync();
        }

        await using var read = db.Context();
        var overview = await new OverviewQueryService(read).GetOverviewAsync();

        Assert.Equal(4, overview.TotalSongs);
        Assert.Equal(5, overview.TotalFinds);
        Assert.Equal(1, overview.FavoritesCount);
        Assert.Equal(2, overview.ArtistsCount);
        Assert.Equal(2, overview.GenresCount);
        Assert.Equal(new[] { "Beta Two", "Beta One", "Beta One", "Alpha One" }, overview.RecentFinds.Select(item => item.Title));
        Assert.DoesNotContain(overview.RecentFinds, item => item.Title == "Alpha Two");
        Assert.Equal(new[] { "Alpha", "Beta" }, overview.TopArtists.Select(item => item.Name));
        Assert.All(overview.TopArtists, item => Assert.Equal(2, item.SongCount));
        Assert.Equal(new[] { "Pop", "Rock" }, overview.TopGenres.Select(item => item.Name));
        Assert.Equal(2, overview.TopGenres[0].SongCount);
        Assert.Equal(new[] { ("Shared Album", "Beta"), ("Older Album", "Alpha") },
            overview.RecentAlbums.Select(item => (item.Name, item.Artist)));
        Assert.Equal("Beta Two", overview.RecentAlbums[0].SongTitle);
    }

    [Fact]
    public async Task EmptyLibraryReturnsZeroCountsAndEmptySections()
    {
        await using var db = await TemporaryDatabase.CreateAsync();
        await using var context = db.Context();
        var overview = await new OverviewQueryService(context).GetOverviewAsync();
        Assert.Equal(0, overview.TotalSongs);
        Assert.Equal(0, overview.TotalFinds);
        Assert.Empty(overview.RecentFinds);
        Assert.Empty(overview.Favorites);
        Assert.Empty(overview.TopArtists);
        Assert.Empty(overview.TopGenres);
        Assert.Empty(overview.RecentAlbums);
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), $"songstash-overview-{Guid.NewGuid():N}");
        private string DatabasePath => Path.Combine(Folder, "library.sqlite");
        public static async Task<TemporaryDatabase> CreateAsync()
        {
            var db = new TemporaryDatabase(); Directory.CreateDirectory(db.Folder);
            await using var context = db.Context(); await context.Database.MigrateAsync(); return db;
        }
        public SongStashDbContext Context() => new(new DbContextOptionsBuilder<SongStashDbContext>().UseSqlite($"Data Source={DatabasePath}").Options);
        public ValueTask DisposeAsync() { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); return ValueTask.CompletedTask; }
    }
}
