using SongStash.Domain.ExternalReferences;
using Xunit;

namespace SongStash.Domain.Tests.ExternalReferences;

public sealed class ExternalReferenceTests
{
    [Fact]
    public void AcceptsExternalIdWithoutUrl()
    {
        var songId = Guid.NewGuid();

        var reference = new ExternalReference(songId, ExternalReferenceProvider.Shazam, "track-key");

        Assert.NotEqual(Guid.Empty, reference.Id);
        Assert.Equal(songId, reference.SongId);
        Assert.Equal(ExternalReferenceProvider.Shazam, reference.Provider);
        Assert.Equal("track-key", reference.ExternalId);
        Assert.Null(reference.Url);
    }

    [Fact]
    public void AcceptsUrlWithoutExternalId()
    {
        var url = new Uri("https://example.test/track");

        var reference = new ExternalReference(Guid.NewGuid(), ExternalReferenceProvider.AppleMusic, url: url);

        Assert.Null(reference.ExternalId);
        Assert.Equal(url, reference.Url);
    }

    [Fact]
    public void AcceptsExternalIdAndUrlTogether()
    {
        var url = new Uri("https://example.test/track");

        var reference = new ExternalReference(Guid.NewGuid(), ExternalReferenceProvider.Isrc, "USAAA0000001", url);

        Assert.Equal("USAAA0000001", reference.ExternalId);
        Assert.Equal(url, reference.Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingExternalIdAndUrl(string? externalId)
    {
        Assert.Throws<ArgumentException>(() =>
            new ExternalReference(Guid.NewGuid(), ExternalReferenceProvider.Shazam, externalId));
    }
}
