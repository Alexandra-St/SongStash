using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SongStash.Infrastructure.Persistence;

public sealed class SongStashDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SongStashDbContext>
{
    public SongStashDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SONGSTASH_DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set SONGSTASH_DB_CONNECTION_STRING to a SQLite connection string for design-time operations.");
        }

        var options = new DbContextOptionsBuilder<SongStashDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new SongStashDbContext(options);
    }
}
