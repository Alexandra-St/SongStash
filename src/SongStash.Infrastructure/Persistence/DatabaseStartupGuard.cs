using Microsoft.EntityFrameworkCore;

namespace SongStash.Infrastructure.Persistence;

/// <summary>Applies pending migrations before the desktop app exposes a library session.</summary>
public static class DatabaseStartupGuard
{
    public static async Task<bool> InitializeAsync(SongStashDbContext database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        try
        {
            await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            return await database.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Startup must fail closed. In particular, never continue as an empty library
            // after an existing database could not be opened or migrated.
            return false;
        }
    }
}
