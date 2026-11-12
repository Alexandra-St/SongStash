using SongStash.Application.Importing;
using SongStash.Application.LibraryActions;
using SongStash.Application.Matching;
using SongStash.Application.Persistence;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using Xunit;

namespace SongStash.Application.Tests.LibraryActions;

public sealed class ManualSongServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task NewManualSongUsesClockAndKeepsFoundAtNullable()
    {
        var fixture = new Fixture();
        var result = await fixture.Service.AddAsync(new ManualSongInput("  Title ", " Artist ", " Album ", " Pop ", " Note "));

        Assert.Equal(ManualAddOutcome.AddedNewSong, result.Outcome);
        var song = Assert.Single(fixture.Committer.Songs);
        Assert.Equal("Title", song.Title);
        Assert.Equal("Artist", song.Artist);
        Assert.Equal("Album", song.Album);
        Assert.Equal("Pop", song.Genre);
        Assert.Equal("Note", song.Notes);
        Assert.Equal(Now, song.CreatedAt);
        Assert.Equal(Now, song.UpdatedAt);
        var find = Assert.Single(fixture.Committer.FindEvents);
        Assert.Equal(FindSource.Manual, find.Source);
        Assert.Equal(Now, find.ImportedAt);
        Assert.Null(find.FoundAt);
    }

    [Theory]
    [InlineData("", "Artist", "Title is required.")]
    [InlineData("Title", "  ", "Artist is required.")]
    public async Task RequiredFieldsAreValidated(string title, string artist, string expected)
    {
        var fixture = new Fixture();
        var result = await fixture.Service.AddAsync(new ManualSongInput(title, artist));
        Assert.Equal(ManualAddOutcome.Invalid, result.Outcome);
        Assert.Equal(expected, result.Message);
        Assert.Empty(fixture.Committer.Songs);
        Assert.Empty(fixture.Committer.FindEvents);
    }

    [Fact]
    public async Task ExactMatchRequiresChoiceAndAttachingAddsOnlyManualFind()
    {
        var original = new Song("Song", "Artist", "Original album", genre: "Rock", notes: "Keep this");
        original.SetFavorite(true);
        var fixture = new Fixture([original]);
        var input = new ManualSongInput("Song", "Artist", "Candidate album", "Pop", "Candidate note", Now.AddDays(-2));

        var suggested = await fixture.Service.AddAsync(input);
        Assert.Equal(ManualAddOutcome.ExactMatchRequiresChoice, suggested.Outcome);
        Assert.Empty(fixture.Committer.Songs);

        var attached = await fixture.Service.AddAsync(input, attachToSongId: original.Id);
        Assert.Equal(ManualAddOutcome.AddedFindToExistingSong, attached.Outcome);
        Assert.Empty(fixture.Committer.Songs);
        var find = Assert.Single(fixture.Committer.FindEvents);
        Assert.Equal(original.Id, find.SongId);
        Assert.Equal(FindSource.Manual, find.Source);
        Assert.Equal(input.FoundAt, find.FoundAt);
        Assert.Equal("Original album", original.Album);
        Assert.Equal("Rock", original.Genre);
        Assert.Equal("Keep this", original.Notes);
        Assert.True(original.IsFavorite);
    }

    [Fact]
    public async Task KeepSeparateCreatesSecondSongOnlyAfterExplicitChoice()
    {
        var existing = new Song("Song", "Artist");
        var fixture = new Fixture([existing]);
        var result = await fixture.Service.AddAsync(new ManualSongInput("Song", "Artist"), keepSeparate: true);
        Assert.Equal(ManualAddOutcome.AddedNewSong, result.Outcome);
        Assert.NotEqual(existing.Id, result.SongId);
        Assert.Single(fixture.Committer.Songs);
        Assert.Single(fixture.Committer.FindEvents);
    }

    [Fact]
    public async Task AmbiguousMatchRequiresExplicitExistingSongOrSeparateDecision()
    {
        var first = new Song("Song", "Artist");
        var second = new Song("Song", "Artist");
        var fixture = new Fixture([first, second]);
        var input = new ManualSongInput("Song", "Artist");

        var needsReview = await fixture.Service.AddAsync(input);
        Assert.Equal(ManualAddOutcome.NeedsReview, needsReview.Outcome);
        Assert.Equal(2, needsReview.Match!.PossibleMatches.Count);
        Assert.Empty(fixture.Committer.FindEvents);

        var attached = await fixture.Service.AddAsync(input, attachToSongId: second.Id);
        Assert.Equal(ManualAddOutcome.AddedFindToExistingSong, attached.Outcome);
        Assert.Equal(second.Id, Assert.Single(fixture.Committer.FindEvents).SongId);
    }

    [Fact]
    public async Task FavoriteActionPersistsRequestedState()
    {
        var song = new Song("Song", "Artist");
        var songs = new FakeSongRepository([song]);
        var actions = new LibraryActionsService(songs, new FakeFindEventRepository());
        Assert.True(await actions.SetFavoriteAsync(song.Id, true));
        Assert.True(songs.Stored[song.Id].IsFavorite);
    }

    private sealed class Fixture
    {
        public FakeSongRepository Songs { get; }
        public FakeFindEventRepository Events { get; } = new();
        public FakeReferences References { get; } = new();
        public FakeCommitter Committer { get; } = new();
        public ManualSongService Service { get; }

        public Fixture(IEnumerable<Song>? initialSongs = null)
        {
            Songs = new FakeSongRepository(initialSongs ?? []);
            Service = new ManualSongService(Songs, References, new SongMatcher(), Committer, new FixedClock());
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class FakeSongRepository(IEnumerable<Song> initial) : ISongRepository
    {
        public Dictionary<Guid, Song> Stored { get; } = initial.ToDictionary(item => item.Id);
        public Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Song>>(Stored.Values.ToArray());
        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Stored.GetValueOrDefault(id));
        public Task AddAsync(Song song, CancellationToken cancellationToken = default) { Stored.Add(song.Id, song); return Task.CompletedTask; }
        public Task UpdateAsync(Song song, CancellationToken cancellationToken = default) { Stored[song.Id] = song; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { Stored.Remove(id); return Task.CompletedTask; }
    }
    private sealed class FakeFindEventRepository : IFindEventRepository
    {
        public Task<IReadOnlyList<FindEvent>> GetForSongsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FindEvent>>([]);
        public Task<IReadOnlyList<FindEvent>> GetBySongIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FindEvent>>([]);
        public Task AddAsync(FindEvent item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class FakeReferences : IExternalReferenceRepository
    {
        public Task<IReadOnlyList<ExternalReference>> GetBySongIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ExternalReference>>([]);
        public Task AddAsync(ExternalReference item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class FakeCommitter : IManualEntryCommitter
    {
        public List<Song> Songs { get; } = [];
        public List<FindEvent> FindEvents { get; } = [];
        public Task CommitNewSongAsync(Song song, FindEvent item, CancellationToken cancellationToken = default) { Songs.Add(song); FindEvents.Add(item); return Task.CompletedTask; }
        public Task CommitFindEventAsync(FindEvent item, CancellationToken cancellationToken = default) { FindEvents.Add(item); return Task.CompletedTask; }
    }
}
