namespace SongStash.Application.Importing;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
