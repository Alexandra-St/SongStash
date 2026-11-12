using SongStash.Application.Library;
using SongStash.Application.LibraryActions;
using SongStash.Application.Persistence;
using SongStash.Desktop.ViewModels;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Application.Settings;
using SongStash.Desktop.Services;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class LibraryViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadsSongsAndShowsFindCounts()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync();

        Assert.Equal(2, viewModel.SongCount);
        Assert.Equal("Found 2 times", Assert.Single(viewModel.Songs, song => song.Id == fixture.First.Id).FindCountText);
        Assert.Equal("Found once", Assert.Single(viewModel.Songs, song => song.Id == fixture.Second.Id).FindCountText);
    }

    [Fact]
    public async Task SearchMatchesTitle()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync();
        viewModel.SearchText = "Moon";

        Assert.Equal(fixture.First.Id, Assert.Single(viewModel.FilteredSongs).Id);
    }

    [Fact]
    public async Task SearchMatchesArtist()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync();
        viewModel.SearchText = "Nina";

        Assert.Equal(fixture.Second.Id, Assert.Single(viewModel.FilteredSongs).Id);
    }

    [Fact]
    public async Task SearchIsCaseInsensitive()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync();
        viewModel.SearchText = "MOON RIVER";

        Assert.Equal(fixture.First.Id, Assert.Single(viewModel.FilteredSongs).Id);
    }

    [Fact]
    public async Task SelectionLoadsSongDetails()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync();
        var row = Assert.Single(viewModel.Songs, song => song.Id == fixture.Second.Id);
        row.SelectCommand.Execute(null);

        Assert.Equal(fixture.Second.Id, viewModel.Details.Id);
        Assert.Equal("Nina Simone", viewModel.Details.Artist);
    }

    [Fact]
    public async Task DetailsContainEveryFindEventAndRepeatFindsStaySeparate()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync();
        Assert.Equal(2, viewModel.Details.FindEvents.Count);
    }

    [Fact]
    public async Task FindEventsAreSortedNewestFirstAndUnknownDatesRemainVisible()
    {
        var fixture = new Fixture(3);
        var viewModel = await fixture.CreateLoadedViewModelAsync();

        Assert.Equal("new", viewModel.Details.FindEvents[0].SourceItemId);
        Assert.Equal("old", viewModel.Details.FindEvents[1].SourceItemId);
        Assert.Equal("unknown", viewModel.Details.FindEvents[2].SourceItemId);
        Assert.Equal("Date unknown", viewModel.Details.FindEvents[2].FoundAt);
    }

    [Fact]
    public async Task DetailsIncludeExternalReferences()
    {
        var fixture = new Fixture(2, withReference: true);
        var viewModel = await fixture.CreateLoadedViewModelAsync();

        Assert.Equal("shazam-key", Assert.Single(viewModel.Details.ExternalReferences).ExternalId);
    }

    [Fact]
    public async Task EmptyLibraryShowsEmptyState()
    {
        var viewModel = await new Fixture(0).CreateLoadedViewModelAsync();

        Assert.True(viewModel.HasNoSongs);
        Assert.Empty(viewModel.FilteredSongs);
        Assert.True(viewModel.HasNoSelection);
    }

    [Fact]
    public async Task ChangingSelectionUpdatesDetails()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync();
        Assert.Equal(fixture.First.Id, viewModel.Details.Id);
        Assert.Single(viewModel.Songs, song => song.Id == fixture.First.Id && song.IsSelected);

        Assert.Single(viewModel.Songs, song => song.Id == fixture.Second.Id).SelectCommand.Execute(null);

        Assert.Equal(fixture.Second.Id, viewModel.Details.Id);
        Assert.Single(viewModel.Songs, song => song.Id == fixture.Second.Id && song.IsSelected);
    }

    [Fact]
    public async Task ClearingSearchRestoresAllSongs()
    {
        var viewModel = await new Fixture(2).CreateLoadedViewModelAsync();
        viewModel.SearchText = "Moon";
        Assert.Single(viewModel.FilteredSongs);

        viewModel.SearchText = string.Empty;

        Assert.Equal(2, viewModel.FilteredSongs.Count);
    }

    [Fact]
    public async Task RecentlyFoundIsTheDefaultSort()
    {
        var viewModel = await CreateSortedViewModelAsync(SortItem("Any song", "Artist", null, null, 0));

        Assert.Equal(LibrarySortMode.RecentlyFound, viewModel.SelectedSortOption.Mode);
    }

    [Fact]
    public async Task RecentlyFoundUsesLatestFindAndTitleForDateTies()
    {
        var viewModel = await CreateSortedViewModelAsync(
            SortItem("Single", "Artist", At(2026, 4, 1), At(2026, 4, 1)),
            SortItem("Repeated", "Artist", At(2026, 5, 1), At(2024, 1, 1), 4),
            SortItem("Same Z", "Artist", At(2026, 4, 1), At(2026, 4, 1)),
            SortItem("Same A", "Artist", At(2026, 4, 1), At(2026, 4, 1)),
            SortItem("No valid date", "Artist", null, null, 2));

        Assert.Equal(new[] { "Repeated", "Same A", "Same Z", "Single", "No valid date" },
            viewModel.FilteredSongs.Select(song => song.Title));
    }

    [Fact]
    public async Task OldestFoundUsesEarliestFindAndPlacesUndatedSongsLast()
    {
        var viewModel = await CreateSortedViewModelAsync(
            SortItem("Later", "Artist", At(2026, 4, 1), At(2026, 4, 1)),
            SortItem("Repeated", "Artist", At(2026, 5, 1), At(2019, 1, 1), 3),
            SortItem("No valid date", "Artist", null, null, 1));

        viewModel.SelectedSortOption = SortOption(viewModel, LibrarySortMode.OldestFound);

        Assert.Equal(new[] { "Repeated", "Later", "No valid date" }, viewModel.FilteredSongs.Select(song => song.Title));
    }

    [Fact]
    public async Task TitleSortsWorkInBothDirections()
    {
        var viewModel = await CreateSortedViewModelAsync(
            SortItem("Charlie", "Artist", null, null, 0),
            SortItem("Alpha", "Artist", null, null, 0),
            SortItem("Bravo", "Artist", null, null, 0));

        viewModel.SelectedSortOption = SortOption(viewModel, LibrarySortMode.TitleAscending);
        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie" }, viewModel.FilteredSongs.Select(song => song.Title));

        viewModel.SelectedSortOption = SortOption(viewModel, LibrarySortMode.TitleDescending);
        Assert.Equal(new[] { "Charlie", "Bravo", "Alpha" }, viewModel.FilteredSongs.Select(song => song.Title));
    }

    [Fact]
    public async Task ArtistSortsHandleTiesAndPutEmptyArtistsLast()
    {
        var viewModel = await CreateSortedViewModelAsync(
            SortItem("Zed", "Nina", null, null, 0),
            SortItem("Alpha", "Nina", null, null, 0),
            SortItem("Bravo", "Adele", null, null, 0),
            SortItem("Empty", "", null, null, 0),
            SortItem("Null", null, null, null, 0));

        viewModel.SelectedSortOption = SortOption(viewModel, LibrarySortMode.ArtistAscending);
        Assert.Equal(new[] { "Bravo", "Alpha", "Zed", "Empty", "Null" }, viewModel.FilteredSongs.Select(song => song.Title));

        viewModel.SelectedSortOption = SortOption(viewModel, LibrarySortMode.ArtistDescending);
        Assert.Equal(new[] { "Alpha", "Zed", "Bravo", "Empty", "Null" }, viewModel.FilteredSongs.Select(song => song.Title));
    }

    [Fact]
    public async Task SearchFiltersBeforeSortAndIsPreservedWhenSortChanges()
    {
        var viewModel = await CreateSortedViewModelAsync(
            SortItem("Alpha", "Nina Simone", null, null, 0),
            SortItem("Zeta", "Nina Simone", null, null, 0),
            SortItem("Middle", "Adele", null, null, 0));
        viewModel.SearchText = "Nina";

        viewModel.SelectedSortOption = SortOption(viewModel, LibrarySortMode.TitleDescending);

        Assert.Equal("Nina", viewModel.SearchText);
        Assert.Equal(new[] { "Zeta", "Alpha" }, viewModel.FilteredSongs.Select(song => song.Title));
    }

    [Fact]
    public async Task ChangingSortKeepsSelectedSongAndDetails()
    {
        var viewModel = await CreateSortedViewModelAsync(
            SortItem("Alpha", "Artist", At(2026, 1, 1), At(2026, 1, 1)),
            SortItem("Zeta", "Artist", At(2026, 2, 1), At(2026, 2, 1)));
        var selectedSong = Assert.Single(viewModel.Songs, song => song.Title == "Zeta");
        selectedSong.SelectCommand.Execute(null);
        var selectedId = selectedSong.Id;
        Assert.Equal("Zeta", viewModel.Details.Title);

        viewModel.SelectedSortOption = SortOption(viewModel, LibrarySortMode.TitleAscending);

        Assert.Equal(selectedId, viewModel.Details.Id);
        Assert.Single(viewModel.Songs, song => song.Id == selectedId && song.IsSelected);
        Assert.Equal(new[] { "Alpha", "Zeta" }, viewModel.FilteredSongs.Select(song => song.Title));
    }

    [Fact]
    public async Task DeleteSongWaitsForConfirmationWhenPreferenceIsEnabled()
    {
        var fixture = new Fixture(2);
        var settings = CreateSettings(confirm: true);
        var songs = new ActionSongRepository([fixture.First, fixture.Second]);
        var vm = new LibraryViewModel(fixture.Service, settings, actions: new LibraryActionsService(songs, new ActionFindEventRepository()));
        await vm.LoadAsync();

        vm.RequestDeleteSongCommand.Execute(null);

        Assert.Equal(fixture.First.Id, vm.PendingDeleteSongId);
        Assert.True(vm.HasPendingDelete);
        Assert.Equal(0, songs.DeleteCalls);
        vm.CancelDeleteCommand.Execute(null);
        Assert.False(vm.HasPendingDelete);
    }

    [Fact]
    public async Task DeleteSongSkipsConfirmationWhenPreferenceIsDisabledAndClearsSelection()
    {
        var fixture = new Fixture(2);
        var songs = new ActionSongRepository([fixture.First, fixture.Second]);
        var query = new MutableLibraryQueryService(songs);
        var vm = new LibraryViewModel(query, CreateSettings(confirm: false),
            actions: new LibraryActionsService(songs, new ActionFindEventRepository()));
        await vm.LoadAsync();

        vm.RequestDeleteSongCommand.Execute(null);

        Assert.Equal(1, songs.DeleteCalls);
        Assert.DoesNotContain(vm.Songs, song => song.Id == fixture.First.Id);
        Assert.Contains(vm.Songs, song => song.Id == fixture.Second.Id);
        Assert.False(vm.HasSelection);
        Assert.True(vm.Details.HasNoSelection);
        Assert.False(vm.HasPendingDelete);
    }

    [Fact]
    public async Task DeleteFindRequestsConfirmationForOnlyTheSelectedFind()
    {
        var fixture = new Fixture(2);
        var vm = new LibraryViewModel(fixture.Service, CreateSettings(confirm: true),
            actions: new LibraryActionsService(new ActionSongRepository([fixture.First, fixture.Second]), new ActionFindEventRepository()));
        await vm.LoadAsync();
        var find = vm.Details.FindEvents[0];

        find.DeleteCommand.Execute(null);

        Assert.Equal(find.Id, vm.PendingDeleteFindEventId);
        Assert.Contains("find", vm.PendingDeleteMessage!, StringComparison.OrdinalIgnoreCase);
    }

    private static SettingsViewModel CreateSettings(bool confirm)
    {
        var settings = new AppSettings { ConfirmDestructiveActions = confirm };
        return new SettingsViewModel(new MemorySettingsStore(), new SettingsLoadResult(settings), new ThemeManager());
    }

    private static async Task<LibraryViewModel> CreateSortedViewModelAsync(params LibrarySongItem[] songs)
    {
        var viewModel = new LibraryViewModel(new FixedLibraryQueryService(songs));
        await viewModel.LoadAsync();
        return viewModel;
    }

    private static LibrarySongItem SortItem(string title, string? artist, DateTimeOffset? latestFoundAt,
        DateTimeOffset? oldestFoundAt, int findCount = 0) =>
        new(Guid.NewGuid(), title, artist!, null, null, false, findCount, latestFoundAt, oldestFoundAt);

    private static DateTimeOffset At(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static LibrarySortOption SortOption(LibraryViewModel viewModel, LibrarySortMode mode) =>
        Assert.Single(viewModel.SortOptions, option => option.Mode == mode);

    [Fact]
    public async Task DelayedDetailsCannotOverwriteNewSelection()
    {
        var first = SortItem("A", "Artist A", null, null);
        var second = SortItem("B", "Artist B", null, null);
        var query = new DelayedDetailsQuery(first, second);
        var vm = new LibraryViewModel(query);
        var loading = vm.LoadAsync();
        await query.FirstRequested.Task;
        var selecting = vm.SelectSongByIdAsync(second.Id);
        Assert.True(vm.Details.HasNoSelection);
        query.Complete(second);
        await selecting;
        Assert.Equal(second.Id, vm.Details.Id);
        query.Complete(first);
        await loading;
        Assert.Equal(second.Id, vm.Details.Id);
        Assert.Single(vm.Songs, row => row.Id == second.Id && row.IsSelected);
    }

    private sealed class DelayedDetailsQuery(LibrarySongItem first, LibrarySongItem second) : ILibraryQueryService
    {
        private readonly Dictionary<Guid, TaskCompletionSource<SongDetails?>> pending = new();
        public TaskCompletionSource FirstRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibrarySongItem>>([first, second]);
        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<SongDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[songId] = completion;
            if (songId == first.Id) FirstRequested.TrySetResult();
            return completion.Task;
        }
        public void Complete(LibrarySongItem song) => pending[song.Id].SetResult(
            new SongDetails(song.Id, song.Title, song.Artist, song.Album, song.ArtworkUrl, null, null, song.IsFavorite, [], []));
    }

    private sealed class FixedLibraryQueryService(IReadOnlyList<LibrarySongItem> songs) : ILibraryQueryService
    {
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(songs);

        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default)
        {
            var song = songs.SingleOrDefault(item => item.Id == songId);
            return Task.FromResult(song is null ? null : new SongDetails(song.Id, song.Title, song.Artist, song.Album,
                song.ArtworkUrl, null, null, song.IsFavorite, [], []));
        }
    }

    private sealed class MutableLibraryQueryService(ActionSongRepository songs) : ILibraryQueryService
    {
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibrarySongItem>>(songs.Stored.Select(song =>
                new LibrarySongItem(song.Id, song.Title, song.Artist ?? string.Empty, song.Album, song.ArtworkUrl,
                    song.IsFavorite, 0, null, null)).ToArray());

        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default)
        {
            var song = songs.Stored.SingleOrDefault(item => item.Id == songId);
            return Task.FromResult(song is null ? null : new SongDetails(song.Id, song.Title, song.Artist, song.Album,
                song.ArtworkUrl, null, null, song.IsFavorite, [], []));
        }
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SettingsLoadResult(new AppSettings()));
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ActionSongRepository(IEnumerable<Song> initial) : ISongRepository
    {
        private readonly Dictionary<Guid, Song> stored = initial.ToDictionary(song => song.Id);
        public IReadOnlyCollection<Song> Stored => stored.Values;
        public int DeleteCalls { get; private set; }
        public Task<IReadOnlyList<Song>> GetAllForMatchingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Song>>(stored.Values.ToArray());
        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(stored.GetValueOrDefault(id));
        public Task AddAsync(Song song, CancellationToken cancellationToken = default) { stored.Add(song.Id, song); return Task.CompletedTask; }
        public Task UpdateAsync(Song song, CancellationToken cancellationToken = default) { stored[song.Id] = song; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { DeleteCalls++; stored.Remove(id); return Task.CompletedTask; }
    }

    private sealed class ActionFindEventRepository : IFindEventRepository
    {
        public Task<IReadOnlyList<FindEvent>> GetForSongsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FindEvent>>([]);
        public Task<IReadOnlyList<FindEvent>> GetBySongIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FindEvent>>([]);
        public Task AddAsync(FindEvent item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task LauncherFailureDoesNotChangeSongOrMakeLibraryUnavailable()
    {
        var fixture = new Fixture(2);
        var viewModel = await fixture.CreateLoadedViewModelAsync(new FailingLauncher());
        var originalTitle = viewModel.Details.Title;
        viewModel.OpenMusicServiceCommand.Execute(MusicService.Spotify);

        Assert.True(viewModel.HasOpenServiceError);
        Assert.Equal(originalTitle, viewModel.Details.Title);
        Assert.Equal(2, viewModel.SongCount);
        Assert.Equal(2, viewModel.FilteredSongs.Count);
    }

    private sealed class Fixture
    {
        public Song First { get; } = new("Moon River", "Andy Williams", null, null, null, null, Now);
        public Song Second { get; } = new("Feeling Good", "Nina Simone", null, null, null, null, Now);
        public List<FindEvent> Events { get; } = [];
        public FakeLibrary Service { get; }

        public Fixture(int eventCount, bool withReference = false)
        {
            if (eventCount > 0)
            {
                Events.Add(new FindEvent(First.Id, FindSource.ShazamApple, Now, Now.AddDays(-2), "old"));
            }
            if (eventCount > 1)
            {
                Events.Add(new FindEvent(First.Id, FindSource.ShazamApple, Now, Now.AddDays(-1), "new"));
            }
            if (eventCount > 2)
            {
                Events.Add(new FindEvent(First.Id, FindSource.ShazamApple, Now, null, "unknown"));
            }
            if (eventCount > 0)
            {
                Events.Add(new FindEvent(Second.Id, FindSource.Manual, Now, Now.AddHours(-1), "second-song"));
            }

            var songs = eventCount == 0 ? Array.Empty<Song>() : [First, Second];
            var references = withReference
                ? new[] { new ExternalReference(First.Id, ExternalReferenceProvider.Shazam, "shazam-key") }
                : Array.Empty<ExternalReference>();
            Service = new FakeLibrary(songs, Events, references);
        }

        public async Task<LibraryViewModel> CreateLoadedViewModelAsync(IExternalUriLauncher? launcher = null)
        {
            var viewModel = new LibraryViewModel(Service, uriLauncher: launcher);
            await viewModel.LoadAsync();
            return viewModel;
        }
    }

    private sealed class FailingLauncher : IExternalUriLauncher
    {
        public void Open(Uri uri) => throw new InvalidOperationException("OS launch details should not reach the user.");
    }

    private sealed class FakeLibrary(
        IReadOnlyList<Song> songs,
        IReadOnlyList<FindEvent> events,
        IReadOnlyList<ExternalReference> references) : ILibraryQueryService
    {
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibrarySongItem>>(songs.Select(song =>
            {
                var songEvents = events.Where(find => find.SongId == song.Id).ToArray();
                var dates = songEvents.Where(find => find.FoundAt.HasValue).Select(find => find.FoundAt!.Value).ToArray();
                return new LibrarySongItem(song.Id, song.Title, song.Artist, song.Album, song.ArtworkUrl,
                    song.IsFavorite, songEvents.Length, dates.Length == 0 ? null : dates.Max(), dates.Length == 0 ? null : dates.Min());
            }).ToArray());

        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default)
        {
            var song = songs.SingleOrDefault(song => song.Id == songId);
            return Task.FromResult(song is null ? null : new SongDetails(
                song.Id, song.Title, song.Artist, song.Album, song.ArtworkUrl, song.Genre, song.Notes,
                song.IsFavorite, events.Where(item => item.SongId == songId)
                    .OrderByDescending(item => item.FoundAt.HasValue)
                    .ThenByDescending(item => item.FoundAt?.UtcDateTime.Ticks ?? long.MinValue)
                    .ThenByDescending(item => item.ImportedAt.UtcDateTime.Ticks)
                    .ThenBy(item => item.Id)
                    .ToArray(),
                references.Where(item => item.SongId == songId).ToArray()));
        }
    }
}
