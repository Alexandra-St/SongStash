using Microsoft.EntityFrameworkCore;
using SongStash.Domain.Songs;
using SongStash.Infrastructure.Persistence;
using Xunit;

namespace SongStash.Infrastructure.Tests.Persistence;

public sealed class DatabaseStartupGuardTests
{
    [Fact]
    public async Task NewDatabaseIsMigratedAndExistingDatabaseIsPreserved()
    {
        await using var database = new TemporaryDatabase();
        await using (var first = database.CreateContext())
        {
            Assert.True(await DatabaseStartupGuard.InitializeAsync(first));
            Assert.True(await first.Database.CanConnectAsync());
            first.Songs.Add(new Song("Existing library entry", "Test artist"));
            await first.SaveChangesAsync();
        }

        await using (var reopened = database.CreateContext())
        {
            Assert.True(await DatabaseStartupGuard.InitializeAsync(reopened));
            Assert.Equal("Existing library entry", (await reopened.Songs.SingleAsync()).Title);
        }
    }

    [Fact]
    public async Task BrokenExistingDatabaseFailsClosedWithoutReplacingItsContents()
    {
        await using var database = new TemporaryDatabase();
        var originalBytes = "not a sqlite database; preserve me"u8.ToArray();
        await File.WriteAllBytesAsync(database.Path, originalBytes);

        await using var context = database.CreateContext();
        Assert.False(await DatabaseStartupGuard.InitializeAsync(context));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(database.Path));
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _directory;
        public TemporaryDatabase()
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"songstash-startup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
        }
        public string Path => System.IO.Path.Combine(_directory, "songstash.db");

        public SongStashDbContext CreateContext() => new(
            new DbContextOptionsBuilder<SongStashDbContext>().UseSqlite($"Data Source={Path}").Options);

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
