using SongStash.Application.Importing;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using Xunit;

namespace SongStash.Application.Tests.Importing;

public sealed class ImportServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NewItemUsesClockAndCommitsSongReferencesAndEvent()
    {
        var fixture = new ServiceFixture();
        var candidate = Candidate("Title", "Artist", sourceItemId: "recognition-1") with
        {
            FoundAt = FixedUtcNow.AddMinutes(-2),
            ExternalReferences =
            [
                new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key"),
                new ExternalReferenceCandidate(ExternalReferenceProvider.Shazam, "track-key")
            ]
        };

        var result = await fixture.Service.ImportAsync([Preview(candidate, ImportPreviewStatus.New)]);

        Assert.Equal(1, result.AddedSongs);
        Assert.Equal(1, result.AddedFindEvents);
        var song = Assert.Single(fixture.Committer.Songs);
        Assert.Equal(FixedUtcNow, song.CreatedAt);
        Assert.Equal(FixedUtcNow, song.UpdatedAt);
        var findEvent = Assert.Single(fixture.Committer.FindEvents);
        Assert.Equal(FixedUtcNow, findEvent.ImportedAt);
        Assert.Equal(candidate.FoundAt, findEvent.FoundAt);
        Assert.Single(fixture.Committer.ExternalReferences);
    }

    [Fact]
    public async Task IncompleteReviewAndIndeterminateItemsNeedAttentionWithoutWrites()
    {
        var fixture = new ServiceFixture();
        var existingSong = new Song("Existing", "Artist");
        fixture.Songs.Stored.Add(existingSong.Id, existingSong);
        var incomplete = Preview(Candidate(" ", "Artist"), ImportPreviewStatus.Incomplete);
        var review = Preview(Candidate("Title", "Artist"), ImportPreviewStatus.NeedsReview);
        var indeterminate = Preview(Candidate("Existing", "Artist"), ImportPreviewStatus.AlreadyInLibrary, existingSong.Id);

        var result = await fixture.Service.ImportAsync([incomplete, review, indeterminate]);

        Assert.Equal(3, result.NeedsAttention);
        Assert.Empty(fixture.Committer.Songs);
        Assert.Empty(fixture.Committer.FindEvents);
    }

    [Fact]
    public async Task DuplicateEventIsSkippedWithoutIncrementingAddedFindEvents()
    {
        var fixture = new ServiceFixture();
        var song = new Song("Title", "Artist");
        fixture.Songs.Stored.Add(song.Id, song);
        fixture.FindEvents.Stored.Add(new FindEvent(song.Id, FindSource.ShazamApple, FixedUtcNow,
            FixedUtcNow.AddMinutes(-1), "event-1"));
        var item = Preview(Candidate("Title", "Artist", sourceItemId: "event-1"),
            ImportPreviewStatus.AlreadyInLibrary, song.Id);

        var result = await fixture.Service.ImportAsync([item]);

        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Equal(0, result.AddedFindEvents);
        Assert.Empty(fixture.Committer.FindEvents);
    }

    [Fact]
    public async Task FailedItemDoesNotStopFollowingItems()
    {
        var fixture = new ServiceFixture();
        fixture.Committer.FailSongTitle = "Fail";
        var items = new[]
        {
            Preview(Candidate("First", "Artist"), ImportPreviewStatus.New),
            Preview(Candidate("Fail", "Artist"), ImportPreviewStatus.New),
            Preview(Candidate("Last", "Artist"), ImportPreviewStatus.New)
        };

        var result = await fixture.Service.ImportAsync(items);

        Assert.Equal(2, result.AddedSongs);
        Assert.Equal(1, result.Failed);
        Assert.Equal(new[] { "First", "Last" }, fixture.Committer.Songs.Select(song => song.Title));
        Assert.DoesNotContain("exception", result.Items[1].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationKeepsCommittedItemAndDoesNotStartNextItem()
    {
        var fixture = new ServiceFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Committer.AfterNewCommit = () => cancellation.Cancel();
        var items = new[]
        {
            Preview(Candidate("First", "Artist"), ImportPreviewStatus.New),
            Preview(Candidate("Second", "Artist"), ImportPreviewStatus.New)
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ImportAsync(items, cancellation.Token));

        Assert.Single(fixture.Committer.Songs);
        Assert.Equal("First", fixture.Committer.Songs[0].Title);
        Assert.Single(fixture.Committer.FindEvents);
    }

    private static ImportCandidate Candidate(string title, string artist, string? sourceItemId = null) =>
        new(title, artist, FindSource.ShazamApple) { SourceItemId = sourceItemId };

    private static ImportPreviewItem Preview(
        ImportCandidate candidate,
        ImportPreviewStatus status,
        Guid? matchedSongId = null) =>
        new(candidate, status, matchedSongId, [], null, candidate.Diagnostics);

    private sealed class ServiceFixture
    {
        public InMemorySongRepository Songs { get; } = new();
        public InMemoryFindEventRepository FindEvents { get; } = new();
        public FakeCommitter Committer { get; } = new();
        public ImportService Service { get; }

        public ServiceFixture() => Service = new ImportService(
            Songs,
            FindEvents,
            Committer,
            new FindEventDeduplicator(),
            new FakeClock(FixedUtcNow));
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class InMemorySongRepository : ISongRepository
    {
        public Dictionary<Guid, Song> Stored { get; } = [];
        public Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>(Stored.Values.ToArray());
        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored.GetValueOrDefault(id));
        public Task AddAsync(Song song, CancellationToken cancellationToken = default)
        {
            Stored.Add(song.Id, song);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(Song song, CancellationToken cancellationToken = default)
        {
            Stored[song.Id] = song;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Stored.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryFindEventRepository : IFindEventRepository
    {
        public List<FindEvent> Stored { get; } = [];
        public Task<IReadOnlyList<FindEvent>> GetForSongsAsync(
            IReadOnlyCollection<Guid> songIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FindEvent>>(Stored.Where(find => songIds.Contains(find.SongId)).ToArray());
        public Task<IReadOnlyList<FindEvent>> GetBySongIdAsync(Guid songId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FindEvent>>(Stored.Where(find => find.SongId == songId).ToArray());
        public Task AddAsync(FindEvent findEvent, CancellationToken cancellationToken = default)
        {
            Stored.Add(findEvent);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Stored.RemoveAll(find => find.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCommitter : IImportItemCommitter
    {
        public List<Song> Songs { get; } = [];
        public List<ExternalReference> ExternalReferences { get; } = [];
        public List<FindEvent> FindEvents { get; } = [];
        public string? FailSongTitle { get; set; }
        public Action? AfterNewCommit { get; set; }

        public Task CommitNewSongAsync(
            Song song,
            IReadOnlyCollection<ExternalReference> externalReferences,
            FindEvent findEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (song.Title == FailSongTitle)
            {
                throw new InvalidOperationException("simulated persistence failure");
            }

            Songs.Add(song);
            ExternalReferences.AddRange(externalReferences);
            FindEvents.Add(findEvent);
            AfterNewCommit?.Invoke();
            return Task.CompletedTask;
        }

        public Task CommitFindEventAsync(FindEvent findEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindEvents.Add(findEvent);
            return Task.CompletedTask;
        }
    }
}
