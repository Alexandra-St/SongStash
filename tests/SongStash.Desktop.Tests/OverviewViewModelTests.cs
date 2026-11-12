using SongStash.Application.Importing;
using SongStash.Application.Library;
using SongStash.Application.Settings;
using SongStash.Desktop.Services;
using SongStash.Desktop.ViewModels;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class OverviewViewModelTests
{
    [Fact]
    public async Task LoadShowsSummaryAndKeepsRepeatFindsAsSeparateRows()
    {
        var songId = Guid.NewGuid();
        var model = new OverviewReadModel(1, 2, 1, 1, 1,
            [
                new OverviewSongItem(songId, Guid.NewGuid(), "Song", "Artist", null, null,
                    new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero)),
                new OverviewSongItem(songId, Guid.NewGuid(), "Song", "Artist", null, null,
                    new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero))
            ],
            [new OverviewSongItem(songId, null, "Song", "Artist", null, null, null)],
            [new OverviewArtistItem("Artist", 1)],
            [new OverviewGenreItem("Pop", 1)],
            [new OverviewAlbumItem("Album", "Artist", "Song", songId, null,
                new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero))]);
        var viewModel = new OverviewViewModel(new FakeOverviewQuery(model));

        await viewModel.LoadAsync();

        Assert.Equal(1, viewModel.TotalSongs);
        Assert.Equal(2, viewModel.TotalFinds);
        Assert.Equal(1, viewModel.FavoritesCount);
        Assert.Equal(1, viewModel.ArtistsCount);
        Assert.Equal(1, viewModel.GenresCount);
        Assert.Equal(2, viewModel.RecentFinds.Count);
        Assert.All(viewModel.RecentFinds, row => Assert.NotNull(row.DateLabel));
        Assert.Single(viewModel.FavoriteSongs);
        Assert.Single(viewModel.RecentAlbums);
        Assert.False(viewModel.IsEmptyLibrary);
    }

    [Fact]
    public async Task EmptyLibraryExposesEmptyStateAndClickingRowsSelectsSong()
    {
        var model = new OverviewReadModel(0, 0, 0, 0, 0, [], [], [], [], []);
        var viewModel = new OverviewViewModel(new FakeOverviewQuery(model));
        await viewModel.LoadAsync();
        Assert.True(viewModel.IsEmptyLibrary);
        Assert.Empty(viewModel.FavoriteSongs);

        var id = Guid.NewGuid();
        var populated = new OverviewViewModel(new FakeOverviewQuery(model with
        {
            Favorites = [new OverviewSongItem(id, null, "Song", "Artist", null, null, null)],
            RecentAlbums = [new OverviewAlbumItem("Album", "Artist", "Song", id, null, DateTimeOffset.UtcNow)]
        }));
        Guid? selected = null;
        populated.SongSelected += value => selected = value;
        await populated.LoadAsync();
        populated.FavoriteSongs[0].SelectCommand.Execute(null);
        Assert.Equal(id, selected);
        populated.RecentAlbums[0].SelectCommand.Execute(null);
        Assert.Equal(id, selected);
    }

    [Fact]
    public async Task LanguageChangeRefreshesOverviewStringsAndExistingSidebarLabels()
    {
        var settings = new SettingsViewModel(new MemorySettingsStore(), new SettingsLoadResult(new AppSettings()), new ThemeManager());
        var songId = Guid.NewGuid();
        var model = new OverviewReadModel(1, 1, 0, 0, 0,
            [new OverviewSongItem(songId, Guid.NewGuid(), "Song", "Artist", null, null, null)], [], [], [], []);
        var overview = new OverviewViewModel(new FakeOverviewQuery(model), settings);
        using var shell = new ShellViewModel(new LibraryViewModel(new EmptyLibraryQuery(), settings),
            new ImportViewModel(new EmptyImportOrchestrator()), settings, overview);
        var overviewStringsNotified = false;
        overview.PropertyChanged += (_, e) => overviewStringsNotified |= e.PropertyName == nameof(OverviewViewModel.Strings);

        await overview.LoadAsync();
        Assert.Equal("Overview", overview.Strings.Title);
        Assert.Equal("Date unknown", overview.RecentFinds[0].DateLabel);

        settings.Language = AppLanguage.Russian;

        Assert.True(overviewStringsNotified);
        Assert.Equal("Обзор", overview.Strings.Title);
        Assert.Equal("Дата неизвестна", overview.RecentFinds[0].DateLabel);
        Assert.Equal("Библиотека", shell.LibraryNavigationLabel);
        Assert.Equal("Обзор", shell.OverviewNavigationLabel);
        Assert.Equal("Import", shell.ImportNavigationLabel);
    }

    [Theory]
    [InlineData(StartPage.Overview, ShellPage.Overview)]
    [InlineData(StartPage.Library, ShellPage.Library)]
    public void SavedStartPageControlsInitialShellPage(StartPage startPage, ShellPage expectedPage)
    {
        var settings = new SettingsViewModel(new MemorySettingsStore(),
            new SettingsLoadResult(new AppSettings { StartPage = startPage }), new ThemeManager());
        using var shell = new ShellViewModel(new LibraryViewModel(new EmptyLibraryQuery()),
            new ImportViewModel(new EmptyImportOrchestrator()), settings,
            new OverviewViewModel(new FakeOverviewQuery(new OverviewReadModel(0, 0, 0, 0, 0, [], [], [], [], []))));

        Assert.Equal(expectedPage, shell.CurrentPage);
    }

    [Fact]
    public async Task SidebarActiveStateTracksTheOpenOverviewLibraryImportAndSettingsPages()
    {
        var settings = new SettingsViewModel(new MemorySettingsStore(),
            new SettingsLoadResult(new AppSettings { StartPage = StartPage.Overview }), new ThemeManager());
        using var shell = new ShellViewModel(new LibraryViewModel(new EmptyLibraryQuery()),
            new ImportViewModel(new EmptyImportOrchestrator()), settings,
            new OverviewViewModel(new FakeOverviewQuery(new OverviewReadModel(0, 0, 0, 0, 0, [], [], [], [], []))));

        Assert.Equal((true, false, false, false), ActivePages(shell));
        await shell.ShowLibraryCommand.ExecuteAsync(null);
        Assert.Equal((false, true, false, false), ActivePages(shell));
        shell.OpenImportCommand.Execute(null);
        Assert.Equal((false, false, true, false), ActivePages(shell));
        shell.CurrentPage = ShellPage.ImportResults;
        Assert.Equal((false, false, true, false), ActivePages(shell));
        shell.OpenSettingsCommand.Execute(null);
        Assert.Equal((false, false, false, true), ActivePages(shell));
    }

    private static (bool Overview, bool Library, bool Import, bool Settings) ActivePages(ShellViewModel shell) =>
        (shell.IsOverviewPage, shell.IsLibraryPage, shell.IsImportPage, shell.IsSettingsPage);

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettingsLoadResult(new AppSettings()));
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptyLibraryQuery : ILibraryQueryService
    {
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibrarySongItem>>([]);
        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SongDetails?>(null);
    }

    private sealed class EmptyImportOrchestrator : IImportOrchestrator
    {
        public Task<ImportPreviewRunResult> ReadPreviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ImportPreviewRunResult.Success([], []));
        public Task<ImportResult> ImportAsync(IEnumerable<ImportPreviewItem> confirmedItems,
            CancellationToken cancellationToken = default) => Task.FromResult(new ImportResult([]));
    }

    private sealed class FakeOverviewQuery(OverviewReadModel model) : IOverviewQueryService
    {
        public Task<OverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default) => Task.FromResult(model);
    }
}
