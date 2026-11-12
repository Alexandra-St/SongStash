using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using System.Globalization;
using SongStash.Application.Backup;
using SongStash.Application.Importing;
using SongStash.Application.Library;
using SongStash.Application.Settings;
using SongStash.Desktop.Services;
using SongStash.Desktop.Views;
using SongStash.Desktop.ViewModels;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ThemeSwitchReplacesSemanticBrushes()
    {
        var resources = new ResourceDictionary();
        ThemePalette.Apply(resources, SongStashTheme.Peachy);
        var peachyAccent = Assert.IsType<SolidColorBrush>(resources["AccentBrush"]).Color;
        ThemePalette.Apply(resources, SongStashTheme.Sage);
        var sageAccent = Assert.IsType<SolidColorBrush>(resources["AccentBrush"]).Color;
        Assert.NotEqual(peachyAccent, sageAccent);
        Assert.Equal(Color.Parse("#FF668D73"), sageAccent);
    }

    [Theory]
    [InlineData(SongStashTheme.Peachy, "#FFF8F0EC", "#FFFCF6F3", "#FFFCF6F3", "#FFF5E9E8")]
    [InlineData(SongStashTheme.Sage, "#FFF0F4EB", "#FFF6F8F1", "#FFF6F8F1", "#FFE8EEE6")]
    public void LightThemesUseTonedBackgroundSidebarAndSurfaces(
        SongStashTheme theme,
        string background,
        string sidebar,
        string surface,
        string secondary)
    {
        var resources = new ResourceDictionary();
        ThemePalette.Apply(resources, theme);

        Assert.Equal(Color.Parse(background), GetBrushColor(resources, "WindowBackgroundBrush"));
        Assert.Equal(Color.Parse(sidebar), GetBrushColor(resources, "SidebarBrush"));
        Assert.Equal(GetBrushColor(resources, "SurfaceBrush"), GetBrushColor(resources, "SidebarBrush"));
        Assert.NotEqual(GetBrushColor(resources, "WindowBackgroundBrush"), GetBrushColor(resources, "SidebarBrush"));
        Assert.Equal(Color.Parse(surface), GetBrushColor(resources, "SurfaceBrush"));
        Assert.Equal(Color.Parse(secondary), GetBrushColor(resources, "SurfaceSecondaryBrush"));
        Assert.NotEqual(Color.Parse("#FFFFFFFF"), GetBrushColor(resources, "SurfaceBrush"));
    }

    [Theory]
    [InlineData(SongStashTheme.Midnight, "#FF101824", "#FF172333", "#FF24364C", "#FF213249", "#FF31445A")]
    [InlineData(SongStashTheme.Afterdark, "#FF121118", "#FF1B1922", "#FF2A2633", "#FF27232E", "#FF393441")]
    public void DarkThemeSurfaceColorsRemainUnchanged(
        SongStashTheme theme,
        string background,
        string surface,
        string secondary,
        string hover,
        string border)
    {
        var resources = new ResourceDictionary();
        ThemePalette.Apply(resources, theme);

        Assert.Equal(Color.Parse(background), GetBrushColor(resources, "WindowBackgroundBrush"));
        Assert.Equal(Color.Parse(surface), GetBrushColor(resources, "SurfaceBrush"));
        Assert.Equal(GetBrushColor(resources, "SurfaceBrush"), GetBrushColor(resources, "SidebarBrush"));
        Assert.Equal(Color.Parse(secondary), GetBrushColor(resources, "SurfaceSecondaryBrush"));
        Assert.Equal(Color.Parse(hover), GetBrushColor(resources, "SurfaceHoverBrush"));
        Assert.Equal(Color.Parse(border), GetBrushColor(resources, "BorderBrush"));
    }

    [Theory]
    [InlineData(SongStashTheme.Peachy)]
    [InlineData(SongStashTheme.Sage)]
    [InlineData(SongStashTheme.Midnight)]
    [InlineData(SongStashTheme.Afterdark)]
    public void SharedButtonAndScrollbarResourcesExistForEveryTheme(SongStashTheme theme)
    {
        var resources = new ResourceDictionary();
        ThemePalette.Apply(resources, theme);

        Assert.IsType<SolidColorBrush>(resources["TextOnAccentBrush"]);
        Assert.IsType<SolidColorBrush>(resources["SidebarBrush"]);
        Assert.IsType<SolidColorBrush>(resources["SurfaceSecondaryBrush"]);
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(resources["FocusRingBrush"]).Color,
            Assert.IsType<SolidColorBrush>(resources["TextControlBorderBrushFocused"]).Color);
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(resources["SelectionBrush"]).Color,
            Assert.IsType<SolidColorBrush>(resources["TextControlSelectionHighlightColor"]).Color);
        Assert.IsType<SolidColorBrush>(resources["AccentHoverBrush"]);
        Assert.IsType<SolidColorBrush>(resources["ScrollBarBackgroundBrush"]);
        Assert.IsType<SolidColorBrush>(resources["ScrollBarThumbBrush"]);
        Assert.IsType<SolidColorBrush>(resources["ScrollBarThumbHoverBrush"]);
        Assert.IsType<SolidColorBrush>(resources["ScrollBarTrackBrush"]);
        Assert.IsType<SolidColorBrush>(resources["StatusErrorTextBrush"]);
        Assert.IsType<SolidColorBrush>(resources["StatusErrorBackgroundBrush"]);
    }

    private static Color GetBrushColor(ResourceDictionary resources, string key) =>
        Assert.IsType<SolidColorBrush>(resources[key]).Color;

    [Theory]
    [InlineData(SongStashTheme.Peachy)]
    [InlineData(SongStashTheme.Sage)]
    [InlineData(SongStashTheme.Midnight)]
    [InlineData(SongStashTheme.Afterdark)]
    public void ToggleSwitchFluentResourcesFollowActiveTheme(SongStashTheme theme)
    {
        var resources = new ResourceDictionary();
        ThemePalette.Apply(resources, theme);

        Assert.Equal(
            Assert.IsType<SolidColorBrush>(resources["AccentBrush"]).Color,
            Assert.IsType<SolidColorBrush>(resources["ToggleSwitchFillOn"]).Color);
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(resources["AccentHoverBrush"]).Color,
            Assert.IsType<SolidColorBrush>(resources["ToggleSwitchFillOnPointerOver"]).Color);
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(resources["SurfaceHoverBrush"]).Color,
            Assert.IsType<SolidColorBrush>(resources["ToggleSwitchFillOff"]).Color);
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(resources["FocusRingBrush"]).Color,
            Assert.IsType<SolidColorBrush>(resources["AccentBrush"]).Color);
    }

    [Theory]
    [InlineData(SongStashTheme.Peachy, false)]
    [InlineData(SongStashTheme.Sage, false)]
    [InlineData(SongStashTheme.Midnight, true)]
    [InlineData(SongStashTheme.Afterdark, true)]
    public void ThemePaletteClassifiesLightAndDarkChoices(SongStashTheme theme, bool isDark) =>
        Assert.Equal(isDark, ThemePalette.IsDark(theme));

    [Fact]
    public async Task AppearanceAndGeneralChoicesUpdateIndependentSettings()
    {
        var store = new MemorySettingsStore();
        var settings = CreateSettingsViewModel(store);
        settings.Theme = SongStashTheme.Midnight;
        settings.Pet = SongStashPet.None;
        settings.ReduceAnimations = true;
        settings.PetAnimationLevel = PetAnimationLevel.Off;
        settings.Language = AppLanguage.Russian;
        settings.StartPage = StartPage.Library;
        settings.LaunchAtStartup = true;
        settings.RememberWindowSizeAndPosition = false;
        settings.NotifyImportComplete = false;
        settings.NotifyImportIssues = false;
        settings.ConfirmDestructiveActions = false;
        await settings.WaitForPendingSaveAsync();

        Assert.Equal(SongStashTheme.Midnight, store.Settings.Theme);
        Assert.Equal(SongStashPet.None, store.Settings.Pet);
        Assert.False(settings.IsPetVisible);
        Assert.True(settings.ReduceAnimations);
        Assert.Equal("Настройки", settings.Strings.Title);
        Assert.Equal(StartPage.Library, store.Settings.StartPage);
        Assert.True(store.Settings.LaunchAtStartup);
        Assert.False(store.Settings.RememberWindowSizeAndPosition);
        Assert.False(store.Settings.NotifyImportComplete);
        Assert.False(store.Settings.NotifyImportIssues);
        Assert.False(store.Settings.ConfirmDestructiveActions);
    }

    [Fact]
    public async Task LanguageSwitchUpdatesCultureAndPersistsAcrossReload()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var store = new MemorySettingsStore();
            var settings = CreateSettingsViewModel(store);
            var languageNotified = false;
            settings.PropertyChanged += (_, e) => languageNotified |= e.PropertyName == nameof(SettingsViewModel.Language);
            Assert.Equal(AppLanguage.English, settings.LanguageOptions[0].Value);
            Assert.Equal("English", settings.LanguageOptions[0].Label);
            Assert.Equal(AppLanguage.Russian, settings.LanguageOptions[1].Value);
            Assert.Equal("Русский", settings.LanguageOptions[1].Label);

            // Mirrors the ComboBox SelectedValue binding: stable enum value, not display text.
            settings.Language = settings.LanguageOptions[1].Value;

            Assert.True(languageNotified);
            Assert.Equal(AppLanguage.Russian, settings.Language);
            Assert.Equal("Русский", settings.LanguageOptions.Single(option => option.Value == settings.Language).Label);
            Assert.Equal("ru-RU", CultureInfo.CurrentCulture.Name);
            Assert.Equal("ru-RU", CultureInfo.CurrentUICulture.Name);
            Assert.Equal("Настройки", settings.Strings.Title);
            await settings.WaitForPendingSaveAsync();
            Assert.Equal(AppLanguage.Russian, store.Settings.Language);

            var reloadedRussian = new SettingsViewModel(store, new SettingsLoadResult(store.Settings), new ThemeManager());
            Assert.Equal(AppLanguage.Russian, reloadedRussian.Language);
            Assert.Equal("ru-RU", CultureInfo.CurrentUICulture.Name);
            Assert.Equal("Настройки", reloadedRussian.Strings.Title);

            reloadedRussian.Language = reloadedRussian.LanguageOptions[0].Value;
            Assert.Equal(AppLanguage.English, reloadedRussian.Language);
            Assert.Equal("English", reloadedRussian.LanguageOptions.Single(option => option.Value == reloadedRussian.Language).Label);
            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Assert.Equal("Settings", reloadedRussian.Strings.Title);
            await reloadedRussian.WaitForPendingSaveAsync();
            var reloadedEnglish = new SettingsViewModel(store, new SettingsLoadResult(store.Settings), new ThemeManager());
            Assert.Equal(AppLanguage.English, reloadedEnglish.Language);
            Assert.Equal("Settings", reloadedEnglish.Strings.Title);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public async Task BackupFrequencySelectionUsesStableValueAcrossLanguageChanges()
    {
        var store = new MemorySettingsStore(new AppSettings
        {
            Language = AppLanguage.English,
            AutomaticBackupFrequency = BackupFrequency.Off
        });
        var backup = new BackupViewModel(new EmptyBackupService(), new SettingsLoadResult(store.Settings),
            new NoOpLauncher(), new FixedClock());
        var settings = new SettingsViewModel(store, new SettingsLoadResult(store.Settings), new ThemeManager(), backup);
        var view = new SettingsView { DataContext = settings };
        var combo = view.FindControl<ComboBox>("BackupFrequencyComboBox")!;

        Assert.Equal(BackupFrequency.Off, combo.SelectedValue);
        Assert.Equal("Off", Assert.IsType<BackupFrequencyOption>(combo.SelectedItem).Label);

        settings.Language = AppLanguage.Russian;

        Assert.Equal(BackupFrequency.Off, backup.AutomaticFrequency);
        Assert.Equal(BackupFrequency.Off, combo.SelectedValue);
        Assert.Equal("Выкл.", Assert.IsType<BackupFrequencyOption>(combo.SelectedItem).Label);
        Assert.Equal("Еженедельно", backup.FrequencyOptions.Single(option => option.Value == BackupFrequency.Weekly).Label);
        Assert.Equal("Ежемесячно", backup.FrequencyOptions.Single(option => option.Value == BackupFrequency.Monthly).Label);

        settings.Language = AppLanguage.English;

        Assert.Equal(BackupFrequency.Off, backup.AutomaticFrequency);
        Assert.Equal(BackupFrequency.Off, combo.SelectedValue);
        Assert.Equal("Off", Assert.IsType<BackupFrequencyOption>(combo.SelectedItem).Label);
        Assert.Equal("Weekly", backup.FrequencyOptions.Single(option => option.Value == BackupFrequency.Weekly).Label);
        Assert.Equal("Monthly", backup.FrequencyOptions.Single(option => option.Value == BackupFrequency.Monthly).Label);
        await settings.WaitForPendingSaveAsync();
        Assert.Equal(BackupFrequency.Off, store.Settings.AutomaticBackupFrequency);
    }

    [Fact]
    public void LibraryAndImportStringsRefreshInBothLanguagesAndKeepSortMode()
    {
        var settings = CreateSettingsViewModel(new MemorySettingsStore());
        var library = new LibraryViewModel(new EmptyLibrary(), settings);
        var import = new ImportViewModel(new EmptyOrchestrator(), settings: settings);

        Assert.Equal("Library", library.Strings.Title);
        Assert.Equal("Recently found", library.SortOptions[0].Label);
        Assert.Equal("Import Music", import.Strings.ImportTitle);
        Assert.Equal("Import Selected", import.Strings.ImportSelected);
        Assert.Equal("The selected import source could not be read.",
            import.Strings.Diagnostic("import.read.failed", "fallback"));

        library.SelectedSortOption = library.SortOptions.Single(option => option.Mode == LibrarySortMode.TitleDescending);
        settings.Language = AppLanguage.Russian;

        Assert.Equal("Библиотека", library.Strings.Title);
        Assert.Equal("Сначала новые", library.SortOptions[0].Label);
        Assert.Equal(LibrarySortMode.TitleDescending, library.SelectedSortOption.Mode);
        Assert.Equal("Название Я–А", library.SelectedSortOption.Label);
        Assert.Equal("Импорт музыки", import.Strings.ImportTitle);
        Assert.Equal("Импортировать выбранное", import.Strings.ImportSelected);
        Assert.Equal("Импорт", import.Strings.NavigationImport);
        Assert.Equal("Не удалось прочитать выбранный источник импорта.",
            import.Strings.Diagnostic("import.read.failed", "fallback"));
        Assert.Equal("Не удалось завершить импорт.",
            import.Strings.Diagnostic("import.operation.failed", "fallback"));

        settings.Language = AppLanguage.English;
        Assert.Equal("Library", library.Strings.Title);
        Assert.Equal("Title Z–A", library.SelectedSortOption.Label);
        Assert.Equal("Import Music", import.Strings.ImportTitle);
    }

    [Fact]
    public async Task PinkCatAndPeachyAreCurrentDefaultsAndPetIsShown()
    {
        var viewModel = CreateSettingsViewModel(new MemorySettingsStore());
        Assert.Equal(SongStashTheme.Peachy, viewModel.Theme);
        Assert.Equal(SongStashPet.PinkCat, viewModel.Pet);
        Assert.True(viewModel.IsPetVisible);
        Assert.Equal("🐱", viewModel.PetGlyph);
        Assert.Equal("English", viewModel.Strings.English);
        Assert.True(viewModel.RememberWindowSizeAndPosition);
        Assert.False(viewModel.LaunchAtStartup);
        await viewModel.WaitForPendingSaveAsync();
    }

    [Fact]
    public void FourSettingsTabsAreAvailable()
    {
        var viewModel = CreateSettingsViewModel(new MemorySettingsStore());
        Assert.True(viewModel.IsAppearanceTab);
        viewModel.CurrentTab = SettingsTab.General;
        Assert.True(viewModel.IsGeneralTab);
        viewModel.CurrentTab = SettingsTab.MusicServices;
        Assert.True(viewModel.IsMusicServicesTab);
        Assert.Equal(MusicService.YouTubeMusic, viewModel.PreferredMusicService);
        Assert.Equal(4, viewModel.MusicServiceRows.Count);
        viewModel.CurrentTab = SettingsTab.Backup;
        Assert.True(viewModel.IsBackupTab);
        Assert.Equal("Backup", viewModel.Strings.BackupTab);
    }

    [Fact]
    public async Task MusicServicesDefaultOrderAndEnabledChoicesArePersisted()
    {
        var store = new MemorySettingsStore();
        var vm = CreateSettingsViewModel(store);
        Assert.Equal(MusicService.YouTubeMusic, vm.PreferredMusicService);
        Assert.Equal(new[] { MusicService.YouTubeMusic, MusicService.YouTube, MusicService.Spotify, MusicService.AppleMusic }, vm.MusicServiceRows.Select(row => row.Service));
        Assert.All(vm.MusicServiceRows, row => Assert.True(row.IsEnabled));

        vm.MusicServiceRows[3].MoveUpCommand.Execute(null);
        vm.PreferredMusicService = MusicService.Spotify;
        Assert.Single(vm.MusicServiceRows, row => row.Service == MusicService.YouTubeMusic).IsEnabled = false;
        await vm.WaitForPendingSaveAsync();
        Assert.Equal("AppleMusic", store.Settings.MusicServiceOrder[2]);
        Assert.DoesNotContain("YouTubeMusic", store.Settings.EnabledMusicServices);
    }

    [Fact]
    public async Task PreferredMusicServiceSelectionPersistsAndSurvivesReorder()
    {
        var store = new MemorySettingsStore();
        var settings = CreateSettingsViewModel(store);
        var enabledBefore = settings.MusicServiceRows.Select(row => row.IsEnabled).ToArray();

        Assert.Equal(MusicService.YouTubeMusic, settings.PreferredMusicService);
        Assert.Contains(settings.MusicServiceOptions, option => option.Service == MusicService.Spotify);

        settings.PreferredMusicService = MusicService.Spotify;
        await settings.WaitForPendingSaveAsync();

        Assert.Equal(MusicService.Spotify, settings.PreferredMusicService);
        Assert.True(Assert.Single(settings.MusicServiceRows, row => row.Service == MusicService.Spotify).IsPreferred);
        Assert.False(Assert.Single(settings.MusicServiceRows, row => row.Service == MusicService.YouTubeMusic).IsPreferred);
        Assert.Equal(nameof(MusicService.Spotify), store.Settings.PreferredMusicService);
        Assert.Equal(enabledBefore, settings.MusicServiceRows.Select(row => row.IsEnabled));

        var reloaded = new SettingsViewModel(store, new SettingsLoadResult(store.Settings), new ThemeManager());
        Assert.Equal(MusicService.Spotify, reloaded.PreferredMusicService);
        Assert.True(Assert.Single(reloaded.MusicServiceRows, row => row.Service == MusicService.Spotify).IsPreferred);

        reloaded.MoveMusicServiceToInsertionBoundary(MusicService.Spotify, reloaded.MusicServiceRows.Count);
        await reloaded.WaitForPendingSaveAsync();
        Assert.Equal(MusicService.Spotify, reloaded.PreferredMusicService);
        Assert.Equal(nameof(MusicService.Spotify), store.Settings.PreferredMusicService);
        Assert.Equal(enabledBefore, reloaded.MusicServiceRows.Select(row => row.IsEnabled));

        var library = new LibraryViewModel(new EmptyLibrary(), reloaded);
        Assert.Equal("Open in Spotify", library.PreferredServiceButtonText);
    }

    [Fact]
    public async Task PreferredMusicServiceComboBoxKeepsSelectionWhenBadgesUpdate()
    {
        var store = new MemorySettingsStore(new AppSettings { PreferredMusicService = nameof(MusicService.Spotify) });
        var settings = CreateSettingsViewModel(store);
        var optionsBefore = settings.MusicServiceOptions.ToDictionary(option => option.Service);
        var view = new SettingsView { DataContext = settings };
        var combo = view.FindControl<ComboBox>("PreferredMusicServiceComboBox")!;

        Assert.Equal(MusicService.Spotify, combo.SelectedValue);
        foreach (var service in new[] { MusicService.YouTube, MusicService.YouTubeMusic, MusicService.Spotify, MusicService.AppleMusic })
        {
            combo.SelectedValue = service;

            Assert.Equal(service, settings.PreferredMusicService);
            Assert.Equal(service, combo.SelectedValue);
            var row = Assert.Single(settings.MusicServiceRows, option => option.Service == service);
            Assert.True(row.IsPreferred);
            Assert.Same(optionsBefore[service], row);
        }

        await settings.WaitForPendingSaveAsync();
        Assert.Equal(nameof(MusicService.AppleMusic), store.Settings.PreferredMusicService);
    }

    [Fact]
    public async Task DragReorderMovesFirstServiceToLastAndPersistsWithoutChangingOtherChoices()
    {
        var store = new MemorySettingsStore();
        var vm = CreateSettingsViewModel(store);
        var enabledBefore = vm.MusicServiceRows.Select(row => row.IsEnabled).ToArray();
        var preferredBefore = vm.PreferredMusicService;

        vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, 4);
        await vm.WaitForPendingSaveAsync();

        Assert.Equal(new[] { MusicService.YouTube, MusicService.Spotify, MusicService.AppleMusic, MusicService.YouTubeMusic }, vm.MusicServiceRows.Select(row => row.Service));
        Assert.Equal(new[] { "YouTube", "Spotify", "AppleMusic", "YouTubeMusic" }, store.Settings.MusicServiceOrder);
        Assert.Equal(preferredBefore, vm.PreferredMusicService);
        Assert.Equal(enabledBefore, vm.MusicServiceRows.Select(row => row.IsEnabled));
    }

    [Fact]
    public async Task DragReorderMovesLastServiceToFirstAndOpenMenuUsesPersistedOrder()
    {
        var store = new MemorySettingsStore();
        var vm = CreateSettingsViewModel(store);
        vm.MoveMusicServiceToInsertionBoundary(MusicService.AppleMusic, 0);
        await vm.WaitForPendingSaveAsync();

        Assert.Equal(new[] { MusicService.AppleMusic, MusicService.YouTubeMusic, MusicService.YouTube, MusicService.Spotify }, vm.MusicServiceRows.Select(row => row.Service));
        var library = new LibraryViewModel(new EmptyLibrary(), vm);
        Assert.Equal(new[] { MusicService.AppleMusic, MusicService.YouTube, MusicService.Spotify }, library.OtherMusicServiceActions.Select(action => action.Service));
    }

    [Fact]
    public void InvalidInsertionBoundaryAndNoOpBoundariesLeaveServiceOrderUnchanged()
    {
        var vm = CreateSettingsViewModel(new MemorySettingsStore());
        var before = vm.MusicServiceRows.Select(row => row.Service).ToArray();

        vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, -1);
        vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, 5);
        vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, 0);
        vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, 1);
        vm.SetMusicServiceDragFeedback(null, null);

        Assert.Equal(before, vm.MusicServiceRows.Select(row => row.Service));
    }

    [Fact]
    public void DragReorderMovesMiddleServiceToRequestedInsertionBoundaryWithoutOffByOne()
    {
        var vm = CreateSettingsViewModel(new MemorySettingsStore());
        vm.MoveMusicServiceToInsertionBoundary(MusicService.Spotify, 1);
        Assert.Equal(new[] { MusicService.YouTubeMusic, MusicService.Spotify, MusicService.YouTube, MusicService.AppleMusic }, vm.MusicServiceRows.Select(row => row.Service));

        vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, 3);
        Assert.Equal(new[] { MusicService.Spotify, MusicService.YouTube, MusicService.YouTubeMusic, MusicService.AppleMusic }, vm.MusicServiceRows.Select(row => row.Service));
    }

    [Fact]
    public void MissingMusicServiceListOrVisualGeometryProducesNoDropAndLeavesOrderUnchanged()
    {
        var vm = CreateSettingsViewModel(new MemorySettingsStore());
        var before = vm.MusicServiceRows.Select(row => row.Service).ToArray();
        var unrootedItems = new ItemsControl { ItemsSource = new[] { "one", "two" } };

        var noNamedControlBoundary = SettingsView.ResolveInsertionBoundary(null, 10);
        var noVisualGeometryBoundary = SettingsView.ResolveInsertionBoundary(unrootedItems, 10);

        Assert.Null(noNamedControlBoundary);
        Assert.Null(noVisualGeometryBoundary);
        if (noNamedControlBoundary is int namedTarget)
            vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, namedTarget);
        if (noVisualGeometryBoundary is int geometryTarget)
            vm.MoveMusicServiceToInsertionBoundary(MusicService.YouTubeMusic, geometryTarget);
        Assert.Equal(before, vm.MusicServiceRows.Select(row => row.Service));
    }

    [Fact]
    public void PreferredAndLastEnabledServicesCannotBeDisabled()
    {
        var vm = CreateSettingsViewModel(new MemorySettingsStore());
        vm.MusicServiceRows[0].IsEnabled = false;
        Assert.Contains("preferred", vm.MusicServiceFeedback!, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.MusicServiceRows[0].IsEnabled);

        var singleService = CreateSettingsViewModel(new MemorySettingsStore());
        singleService.MusicServiceRows[1].IsEnabled = false;
        singleService.MusicServiceRows[2].IsEnabled = false;
        singleService.MusicServiceRows[3].IsEnabled = false;
        singleService.MusicServiceRows[0].IsEnabled = false;
        Assert.True(singleService.MusicServiceRows[0].IsEnabled);
    }

    [Fact]
    public void InvalidPersistedMusicServiceSettingsNormalizeSafely()
    {
        var original = new AppSettings
        {
            Theme = SongStashTheme.Sage,
            PreferredMusicService = "FuturePlayer",
            MusicServiceOrder = ["Spotify", "Spotify", "bad", "YouTube"],
            EnabledMusicServices = ["FuturePlayer", "Spotify", "Spotify"]
        };
        var vm = new SettingsViewModel(new MemorySettingsStore(), new SettingsLoadResult(original), new ThemeManager());
        Assert.Equal(SongStashTheme.Sage, vm.Theme);
        Assert.Equal(MusicService.Spotify, vm.PreferredMusicService);
        Assert.Equal(4, vm.MusicServiceRows.Count);
        Assert.Single(vm.MusicServiceRows, row => row.Service == MusicService.Spotify);
        Assert.Equal(1, vm.MusicServiceRows.Count(row => row.IsEnabled));
    }

    [Fact]
    public async Task LibraryActionUsesCurrentPreferredAndShowsOnlyOtherEnabledServices()
    {
        var settings = CreateSettingsViewModel(new MemorySettingsStore());
        var library = new LibraryViewModel(new EmptyLibrary(), settings);
        Assert.Equal("Open in YouTube Music", library.PreferredServiceButtonText);
        Assert.Equal(3, library.OtherMusicServiceActions.Count);

        settings.PreferredMusicService = MusicService.Spotify;
        Assert.Equal("Open in Spotify", library.PreferredServiceButtonText);
        Assert.DoesNotContain(library.OtherMusicServiceActions, action => action.Service == MusicService.Spotify);
        Assert.Single(settings.MusicServiceRows, row => row.Service == MusicService.YouTube).IsEnabled = false;
        Assert.DoesNotContain(library.OtherMusicServiceActions, action => action.Service == MusicService.YouTube);
        await settings.WaitForPendingSaveAsync();
    }

    [Fact]
    public void ShellSettingsNavigationOpensSettingsPage()
    {
        using var shell = new ShellViewModel(new LibraryViewModel(new EmptyLibrary()),
            new ImportViewModel(new EmptyOrchestrator()), CreateSettingsViewModel(new MemorySettingsStore()));
        shell.OpenSettingsCommand.Execute(null);
        Assert.Equal(ShellPage.Settings, shell.CurrentPage);
        Assert.True(shell.IsSettingsPage);
    }

    [Fact]
    public async Task SettingsEntryResetsToAppearanceOnlyAfterLeavingSettings()
    {
        var settings = CreateSettingsViewModel(new MemorySettingsStore());
        using var shell = new ShellViewModel(new LibraryViewModel(new EmptyLibrary()),
            new ImportViewModel(new EmptyOrchestrator()), settings);

        shell.OpenSettingsCommand.Execute(null);
        Assert.Equal(SettingsTab.Appearance, settings.CurrentTab);

        settings.CurrentTab = SettingsTab.MusicServices;
        shell.OpenSettingsCommand.Execute(null);
        Assert.Equal(SettingsTab.MusicServices, settings.CurrentTab);

        await shell.ShowLibraryCommand.ExecuteAsync(null);
        shell.OpenSettingsCommand.Execute(null);
        Assert.Equal(SettingsTab.Appearance, settings.CurrentTab);

        settings.CurrentTab = SettingsTab.Backup;
        await shell.ShowOverviewCommand.ExecuteAsync(null);
        shell.OpenSettingsCommand.Execute(null);
        Assert.Equal(SettingsTab.Appearance, settings.CurrentTab);

        settings.CurrentTab = SettingsTab.General;
        shell.OpenImportCommand.Execute(null);
        shell.OpenSettingsCommand.Execute(null);
        Assert.Equal(SettingsTab.Appearance, settings.CurrentTab);
    }

    [Fact]
    public void SystemThemeCanSelectLightAndDarkPalettesAtStartup()
    {
        var settings = new AppSettings
        {
            FollowSystemTheme = true,
            LightTheme = SongStashTheme.Sage,
            DarkTheme = SongStashTheme.Midnight
        };
        Assert.Equal(SongStashTheme.Sage, settings.LightTheme);
        Assert.Equal(SongStashTheme.Midnight, settings.DarkTheme);
        Assert.False(ThemePalette.IsDark(settings.LightTheme));
        Assert.True(ThemePalette.IsDark(settings.DarkTheme));
        Assert.Equal(SongStashTheme.Sage, ThemePalette.Resolve(settings, systemIsDark: false));
        Assert.Equal(SongStashTheme.Midnight, ThemePalette.Resolve(settings, systemIsDark: true));
    }

    [Fact]
    public async Task ThemeCardsSelectTheAppliedThemeWhenSystemFollowingIsOff()
    {
        var store = new MemorySettingsStore(new AppSettings { Theme = SongStashTheme.Peachy });
        var settings = new SettingsViewModel(store, new SettingsLoadResult(store.Settings),
            new ThemeManager(() => false));

        foreach (var theme in new[] { SongStashTheme.Peachy, SongStashTheme.Sage, SongStashTheme.Midnight, SongStashTheme.Afterdark })
        {
            settings.SelectThemeCommand.Execute(theme);
            Assert.Equal(theme, settings.ActiveTheme);
            AssertSelectedTheme(settings, theme);
        }

        await settings.WaitForPendingSaveAsync();
        Assert.Equal(SongStashTheme.Afterdark, store.Settings.Theme);
    }

    [Fact]
    public async Task AppearanceXamlToggleAndThemeCardCommandsStayConnectedAfterTurningSystemFollowingOff()
    {
        Assert.Equal(BindingMode.TwoWay, ToggleSwitch.IsCheckedProperty
            .GetMetadata(typeof(ToggleSwitch)).DefaultBindingMode);
        var store = new MemorySettingsStore(new AppSettings
        {
            Theme = SongStashTheme.Peachy,
            FollowSystemTheme = true,
            LightTheme = SongStashTheme.Peachy,
            DarkTheme = SongStashTheme.Afterdark
        });
        var settings = new SettingsViewModel(store, new SettingsLoadResult(store.Settings),
            new ThemeManager(() => false));
        var view = new SettingsView { DataContext = settings };
        var toggle = view.FindControl<ToggleSwitch>("FollowSystemThemeToggle")!;

        Assert.True(toggle.IsChecked);
        toggle.IsChecked = false;
        Assert.False(settings.FollowSystemTheme);

        var cards = new (string Name, SongStashTheme Theme)[]
        {
            ("SageThemeCard", SongStashTheme.Sage),
            ("MidnightThemeCard", SongStashTheme.Midnight),
            ("AfterdarkThemeCard", SongStashTheme.Afterdark),
            ("PeachyThemeCard", SongStashTheme.Peachy)
        };

        foreach (var (name, theme) in cards)
        {
            var card = view.FindControl<Button>(name)!;
            Assert.Equal(theme, card.CommandParameter);
            Assert.Same(settings.SelectThemeCommand, card.Command);
            card.Command!.Execute(card.CommandParameter);
            Assert.Equal(theme, settings.CurrentSettings.Theme);
            Assert.Equal(theme, settings.ActiveTheme);
            AssertSelectedTheme(settings, theme);
        }

        toggle.IsChecked = true;
        Assert.True(settings.FollowSystemTheme);
        Assert.False(view.FindControl<Button>("PeachyThemeCard")!.IsEnabled);
        Assert.False(view.FindControl<Button>("SageThemeCard")!.IsEnabled);
        Assert.False(view.FindControl<Button>("MidnightThemeCard")!.IsEnabled);
        Assert.False(view.FindControl<Button>("AfterdarkThemeCard")!.IsEnabled);
        Assert.Equal(SongStashTheme.Peachy, settings.ActiveTheme);
        view.FindControl<Button>("MidnightThemeCard")!.Command!.Execute(SongStashTheme.Midnight);
        Assert.Equal(SongStashTheme.Peachy, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Peachy);

        toggle.IsChecked = false;
        Assert.False(settings.FollowSystemTheme);
        view.FindControl<Button>("SageThemeCard")!.Command!.Execute(SongStashTheme.Sage);
        Assert.Equal(SongStashTheme.Sage, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Sage);
        await settings.WaitForPendingSaveAsync();
    }

    [Fact]
    public async Task FollowSystemThemeCardsReflectEffectiveThemeAndIgnoreDirectThemeClicks()
    {
        var systemIsDark = false;
        var store = new MemorySettingsStore(new AppSettings
        {
            Theme = SongStashTheme.Peachy,
            FollowSystemTheme = true,
            LightTheme = SongStashTheme.Peachy,
            DarkTheme = SongStashTheme.Afterdark
        });
        var settings = new SettingsViewModel(store, new SettingsLoadResult(store.Settings),
            new ThemeManager(() => systemIsDark));

        Assert.Equal(SongStashTheme.Peachy, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Peachy);
        settings.SelectThemeCommand.Execute(SongStashTheme.Midnight);
        Assert.Equal(SongStashTheme.Peachy, settings.Theme);
        Assert.Equal(SongStashTheme.Peachy, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Peachy);

        settings.LightTheme = SongStashTheme.Sage;
        Assert.Equal(SongStashTheme.Sage, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Sage);

        systemIsDark = true;
        settings.ApplySystemThemeChange();
        Assert.Equal(SongStashTheme.Afterdark, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Afterdark);

        settings.DarkTheme = SongStashTheme.Midnight;
        Assert.Equal(SongStashTheme.Midnight, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Midnight);
        await settings.WaitForPendingSaveAsync();

        Assert.Equal(SongStashTheme.Sage, store.Settings.LightTheme);
        Assert.Equal(SongStashTheme.Midnight, store.Settings.DarkTheme);
        Assert.Equal(SongStashTheme.Peachy, store.Settings.Theme);
    }

    [Fact]
    public void SwitchingSystemFollowingOnAndOffAlwaysSelectsTheAppliedTheme()
    {
        var store = new MemorySettingsStore(new AppSettings
        {
            Theme = SongStashTheme.Sage,
            FollowSystemTheme = false,
            LightTheme = SongStashTheme.Peachy,
            DarkTheme = SongStashTheme.Afterdark
        });
        var settings = new SettingsViewModel(store, new SettingsLoadResult(store.Settings),
            new ThemeManager(() => true));

        Assert.Equal(SongStashTheme.Sage, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Sage);

        settings.FollowSystemTheme = true;
        Assert.Equal(SongStashTheme.Afterdark, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Afterdark);

        settings.FollowSystemTheme = false;
        Assert.Equal(SongStashTheme.Sage, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Sage);

        settings.Theme = SongStashTheme.Midnight;
        Assert.Equal(SongStashTheme.Midnight, settings.ActiveTheme);
        AssertSelectedTheme(settings, SongStashTheme.Midnight);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemThemeTransitionsAndMappingsRemainConsistent(bool systemIsDark)
    {
        var store = new MemorySettingsStore(new AppSettings { Theme = SongStashTheme.Sage, LightTheme = SongStashTheme.Peachy, DarkTheme = SongStashTheme.Afterdark });
        var vm = new SettingsViewModel(store, new SettingsLoadResult(store.Settings), new ThemeManager(() => systemIsDark));
        Assert.Equal(SongStashTheme.Sage, vm.ActiveTheme);
        vm.FollowSystemTheme = true;
        Assert.Equal(systemIsDark ? SongStashTheme.Afterdark : SongStashTheme.Peachy, vm.ActiveTheme);
        vm.LightTheme = SongStashTheme.Sage;
        vm.DarkTheme = SongStashTheme.Midnight;
        Assert.Equal(systemIsDark ? SongStashTheme.Midnight : SongStashTheme.Sage, vm.ActiveTheme);
        systemIsDark = !systemIsDark;
        vm.ApplySystemThemeChange();
        Assert.Equal(systemIsDark ? SongStashTheme.Midnight : SongStashTheme.Sage, vm.ActiveTheme);
        systemIsDark = !systemIsDark;
        vm.ApplySystemThemeChange();
        Assert.Equal(systemIsDark ? SongStashTheme.Midnight : SongStashTheme.Sage, vm.ActiveTheme);
        vm.FollowSystemTheme = false;
        systemIsDark = !systemIsDark;
        vm.ApplySystemThemeChange();
        Assert.Equal(SongStashTheme.Sage, vm.ActiveTheme);
        await vm.WaitForPendingSaveAsync();
    }

    private static void AssertSelectedTheme(SettingsViewModel settings, SongStashTheme theme)
    {
        Assert.Equal(theme == SongStashTheme.Peachy, settings.IsPeachySelected);
        Assert.Equal(theme == SongStashTheme.Sage, settings.IsSageSelected);
        Assert.Equal(theme == SongStashTheme.Midnight, settings.IsMidnightSelected);
        Assert.Equal(theme == SongStashTheme.Afterdark, settings.IsAfterdarkSelected);
    }

    private static SettingsViewModel CreateSettingsViewModel(MemorySettingsStore store) =>
        new(store, new SettingsLoadResult(store.Settings), new ThemeManager());

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public MemorySettingsStore(AppSettings? settings = null) => Settings = settings ?? new AppSettings();
        public AppSettings Settings { get; private set; }
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettingsLoadResult(Settings));
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyLibrary : ILibraryQueryService
    {
        public Task<IReadOnlyList<LibrarySongItem>> GetSongsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibrarySongItem>>([]);
        public Task<SongDetails?> GetSongDetailsAsync(Guid songId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SongDetails?>(null);
    }

    private sealed class EmptyOrchestrator : IImportOrchestrator
    {
        public Task<ImportPreviewRunResult> ReadPreviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ImportPreviewRunResult.Success([], []));
        public Task<ImportPreviewRunResult> ReadPreviewAsync(IImportSourceReader sourceReader,
            CancellationToken cancellationToken = default) => ReadPreviewAsync(cancellationToken);
        public Task<ImportResult> ImportAsync(IEnumerable<ImportPreviewItem> confirmedItems,
            CancellationToken cancellationToken = default) => Task.FromResult(new ImportResult([]));
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }
    private sealed class NoOpLauncher : IExternalUriLauncher { public void Open(Uri uri) { } }
    private sealed class EmptyBackupService : IBackupService
    {
        public Task<BackupOperationResult> CreateAsync(string folderPath, CancellationToken cancellationToken = default) => Task.FromResult(new BackupOperationResult(true, null));
        public Task<BackupInspectionResult> InspectAsync(string backupPath, CancellationToken cancellationToken = default) => Task.FromResult(new BackupInspectionResult(false, null, null));
        public Task<BackupOperationResult> RestoreAsync(string backupPath, BackupRestoreMode mode, bool restoreSettings, CancellationToken cancellationToken = default) => Task.FromResult(new BackupOperationResult(false, null));
        public Task<IReadOnlyList<BackupInfo>> ListAsync(string folderPath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BackupInfo>>([]);
        public Task<bool> DeleteAsync(string backupPath, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
