using SongStash.Application.Library;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using Xunit;

namespace SongStash.Application.Tests.Library;

public sealed class LibraryQueryServiceTests
{
    [Fact]
    public async Task SongItemUsesLatestAndOldestFindEventDates()
    {
        var song = new Song("Repeated", "Artist");
        var first = new DateTimeOffset(2024, 1, 2, 10, 0, 0, TimeSpan.Zero);
        var latest = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var service = new LibraryQueryService(
            new FakeSongRepository([song]),
            new FakeFindEventRepository([
                new FindEvent(song.Id, FindSource.Manual, latest, latest),
                new FindEvent(song.Id, FindSource.ShazamApple, latest, first)
            ]),
            new FakeReferenceRepository());

        var item = Assert.Single(await service.GetSongsAsync());

        Assert.Equal(2, item.FindCount);
        Assert.Equal(latest, item.LatestFoundAt);
        Assert.Equal(first, item.OldestFoundAt);
    }

    private sealed class FakeSongRepository(IReadOnlyList<Song> initial) : ISongRepository
    {
        public Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default) => Task.FromResult(initial);
        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(initial.SingleOrDefault(song => song.Id == id));
        public Task AddAsync(Song song, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Song song, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFindEventRepository(IReadOnlyList<FindEvent> events) : IFindEventRepository
    {
        public Task<IReadOnlyList<FindEvent>> GetForSongsAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FindEvent>>(events.Where(item => songIds.Contains(item.SongId)).ToArray());
        public Task<IReadOnlyList<FindEvent>> GetBySongIdAsync(Guid songId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FindEvent>>(events.Where(item => item.SongId == songId).ToArray());
        public Task AddAsync(FindEvent findEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeReferenceRepository : IExternalReferenceRepository
    {
        public Task<IReadOnlyList<ExternalReference>> GetBySongIdsAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExternalReference>>([]);
        public Task AddAsync(ExternalReference reference, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
