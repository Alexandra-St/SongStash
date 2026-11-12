using SongStash.Domain.Tags;
using Xunit;

namespace SongStash.Domain.Tests.Tags;

public sealed class TagTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsEmptyName(string name)
    {
        Assert.Throws<ArgumentException>(() => new Tag(name));
    }

    [Fact]
    public void NormalizationIsCaseInsensitiveDeterministicAndKeepsDisplayName()
    {
        var first = new Tag("  Road Trip ");
        var second = new Tag("road trip");

        Assert.Equal("  Road Trip ", first.Name);
        Assert.Equal(first.NormalizedName, second.NormalizedName);
        Assert.Equal("ROAD TRIP", first.NormalizedName);
    }
}
