using SongStash.Application.Persistence;

namespace SongStash.Application.Library;

public sealed class LibraryQueryService(
    ISongRepository songs,
    IFindEventRepository findEvents,
    IExternalReferenceRepository externalReferences) : ILibraryQueryService
{
    public async Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default)
    {
        var librarySongs = await songs.GetAllForMatchingAsync(cancellationToken).ConfigureAwait(false);
        var ids = librarySongs.Select(song => song.Id).ToArray();
        var allEvents = ids.Length == 0
            ? []
            : await findEvents.GetForSongsAsync(ids, cancellationToken).ConfigureAwait(false);
        var eventsBySong = allEvents.GroupBy(item => item.SongId).ToDictionary(group => group.Key, group => group.ToArray());

        return librarySongs
            .OrderBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.Artist, StringComparer.CurrentCultureIgnoreCase)
            .Select(song =>
            {
                var songEvents = eventsBySong.GetValueOrDefault(song.Id) ?? [];
                var foundDates = songEvents.Where(item => item.FoundAt.HasValue)
                    .Select(item => item.FoundAt!.Value)
                    .ToArray();
                return new LibrarySongItem(
                    song.Id, song.Title, song.Artist, song.Album, song.ArtworkUrl,
                    song.IsFavorite, songEvents.Length,
                    foundDates.Length == 0 ? null : foundDates.Max(),
                    foundDates.Length == 0 ? null : foundDates.Min());
            })
            .ToArray();
    }

    public async Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default)
    {
        var song = await songs.GetByIdAsync(songId, cancellationToken).ConfigureAwait(false);
        if (song is null)
        {
            return null;
        }

        var events = await findEvents.GetBySongIdAsync(songId, cancellationToken).ConfigureAwait(false);
        var references = await externalReferences.GetBySongIdsAsync([songId], cancellationToken).ConfigureAwait(false);
        var orderedEvents = events
            .OrderByDescending(item => item.FoundAt.HasValue)
            .ThenByDescending(item => item.FoundAt?.UtcDateTime.Ticks ?? long.MinValue)
            .ThenByDescending(item => item.ImportedAt.UtcDateTime.Ticks)
            .ThenBy(item => item.Id)
            .ToArray();

        return new SongDetails(
            song.Id, song.Title, song.Artist, song.Album, song.ArtworkUrl, song.Genre,
            song.Notes, song.IsFavorite, orderedEvents, references);
    }
}
