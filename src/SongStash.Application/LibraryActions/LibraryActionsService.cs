using SongStash.Application.Persistence;

namespace SongStash.Application.LibraryActions;

public sealed class LibraryActionsService(ISongRepository songs, IFindEventRepository findEvents)
{
    public async Task<bool> SetFavoriteAsync(Guid songId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var song = await songs.GetByIdAsync(songId, cancellationToken).ConfigureAwait(false);
        if (song is null) return false;
        song.SetFavorite(isFavorite);
        await songs.UpdateAsync(song, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task DeleteSongAsync(Guid songId, CancellationToken cancellationToken = default) =>
        songs.DeleteAsync(songId, cancellationToken);

    public Task DeleteFindEventAsync(Guid findEventId, CancellationToken cancellationToken = default) =>
        findEvents.DeleteAsync(findEventId, cancellationToken);
}
