using SongStash.Domain.Songs;
using Xunit;

namespace SongStash.Domain.Tests.Songs;

public sealed class SongTests
{
    [Fact]
    public void CreatesSongWithRequiredTextWithoutRewritingIt()
    {
        var song = new Song("  Track feat. Guest  ", " Artist ");

        Assert.NotEqual(Guid.Empty, song.Id);
        Assert.Equal("  Track feat. Guest  ", song.Title);
        Assert.Equal(" Artist ", song.Artist);
        Assert.True(song.CreatedAt.Offset == TimeSpan.Zero);
        Assert.Equal(song.CreatedAt, song.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsEmptyTitle(string title)
    {
        Assert.Throws<ArgumentException>(() => new Song(title, "Artist"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsEmptyArtist(string artist)
    {
        Assert.Throws<ArgumentException>(() => new Song("Title", artist));
    }

    [Fact]
    public void OptionalMetadataCanBeNull()
    {
        var song = new Song("Title", "Artist");

        Assert.Null(song.Album);
        Assert.Null(song.ArtworkUrl);
        Assert.Null(song.Genre);
        Assert.Null(song.Notes);
        Assert.False(song.IsFavorite);
    }

    [Fact]
    public void UpdateDetailsPreservesTextAndRejectsInvalidRequiredFields()
    {
        var song = new Song("Title", "Artist");

        song.UpdateDetails(" Live Version ", " Artist ", null, null, null, null);

        Assert.Equal(" Live Version ", song.Title);
        Assert.Equal(" Artist ", song.Artist);
        Assert.Throws<ArgumentException>(() => song.UpdateDetails(" ", "Artist", null, null, null, null));
    }
}
