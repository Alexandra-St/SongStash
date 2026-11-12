using SongStash.Domain.Finds;
using Xunit;

namespace SongStash.Domain.Tests.Finds;

public sealed class FindEventTests
{
    [Fact]
    public void FoundAtCanBeNullAndDoesNotReplaceImportedAt()
    {
        var importedAt = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var find = new FindEvent(Guid.NewGuid(), FindSource.Manual, importedAt);

        Assert.Null(find.FoundAt);
        Assert.Equal(importedAt, find.ImportedAt);
    }

    [Fact]
    public void SameSongCanHaveMultipleFindEvents()
    {
        var songId = Guid.NewGuid();
        var importedAt = DateTimeOffset.UtcNow;
        var first = new FindEvent(songId, FindSource.ShazamApple, importedAt,
            importedAt.AddMinutes(-2), "recognition-a");
        var second = new FindEvent(songId, FindSource.ShazamApple, importedAt,
            importedAt.AddMinutes(-1), "recognition-b");

        Assert.Equal(first.SongId, second.SongId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.SourceItemId, second.SourceItemId);
        Assert.NotEqual(first.FoundAt, second.FoundAt);
    }
}
