using SongStash.Application.Importing;

namespace SongStash.Infrastructure.Persistence;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
