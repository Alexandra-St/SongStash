namespace SongStash.Application.Library;

public interface ILibraryQueryService
{
    Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default);
    Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default);
}
