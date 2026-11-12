using SongStash.Application.MusicServices;
using SongStash.Application.Settings;
using SongStash.Domain.ExternalReferences;
using Xunit;

namespace SongStash.Application.Tests.MusicServices;

public sealed class MusicServiceLinkResolverTests
{
    private readonly MusicServiceLinkResolver _resolver = new();

    [Theory]
    [InlineData(MusicService.YouTubeMusic, "https://music.youtube.com/search?q=The%20Beatles%20Here%20Comes%20the%20Sun")]
    [InlineData(MusicService.YouTube, "https://www.youtube.com/results?search_query=The%20Beatles%20Here%20Comes%20the%20Sun")]
    [InlineData(MusicService.Spotify, "https://open.spotify.com/search/The%20Beatles%20Here%20Comes%20the%20Sun")]
    [InlineData(MusicService.AppleMusic, "https://music.apple.com/us/search?term=The%20Beatles%20Here%20Comes%20the%20Sun")]
    public void SearchUsesArtistAndTitleWithEncodedQuery(MusicService service, string expected) =>
        Assert.Equal(expected, _resolver.Resolve("Here Comes the Sun", "The Beatles", [], service).AbsoluteUri);

    [Fact]
    public void TitleOnlyFallbackIsUsedWhenArtistIsMissing() =>
        Assert.Equal("https://www.youtube.com/results?search_query=Song%20%26%20Title",
            _resolver.Resolve("Song & Title", " ", [], MusicService.YouTube).AbsoluteUri);

    [Fact]
    public void AppleMusicDirectReferenceIsPreferredWhenItIsAValidAppleMusicUrl()
    {
        var reference = new ExternalReference(Guid.NewGuid(), ExternalReferenceProvider.AppleMusic,
            "123", new Uri("https://music.apple.com/us/album/example/123"));
        Assert.Equal(reference.Url, _resolver.Resolve("Track", "Artist", [reference], MusicService.AppleMusic));
    }

    [Fact]
    public void AppleMusicIdWithoutUrlUsesSearchFallback()
    {
        var reference = new ExternalReference(Guid.NewGuid(), ExternalReferenceProvider.AppleMusic, "123");
        Assert.Equal("https://music.apple.com/us/search?term=Artist%20Track",
            _resolver.Resolve("Track", "Artist", [reference], MusicService.AppleMusic).AbsoluteUri);
    }

    [Fact]
    public void ShazamReferenceIsNeverTreatedAsAnotherServiceId()
    {
        var reference = new ExternalReference(Guid.NewGuid(), ExternalReferenceProvider.Shazam,
            "unrelated-shazam-key", new Uri("https://www.shazam.com/track/123"));
        Assert.Equal("https://open.spotify.com/search/Artist%20Track",
            _resolver.Resolve("Track", "Artist", [reference], MusicService.Spotify).AbsoluteUri);
    }
}
