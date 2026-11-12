using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Settings;
using System.Collections.ObjectModel;
using System.Globalization;
using SongStash.Desktop.Services;

namespace SongStash.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly ThemeManager _themeManager;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private AppSettings _settings;
    private Task _saveTask = Task.CompletedTask;
    private SongStashTheme _activeTheme;
    private SettingsTab _currentTab;
    private SongStashTheme _lightTheme;
    private SongStashTheme _darkTheme;
    private SongStashPet _pet;
    private PetAnimationLevel _petAnimationLevel;
    private bool _reduceAnimations;
    private AppLanguage _language;
    private bool _launchAtStartup;
    private bool _rememberWindowSizeAndPosition;
    private StartPage _startPage;
    private bool _notifyImportComplete;
    private bool _notifyImportIssues;
    private bool _confirmDestructiveActions;
    private bool _hasSaveError;

    public SettingsViewModel(ISettingsStore store, SettingsLoadResult loadResult, ThemeManager themeManager, BackupViewModel? backup = null)
    {
        _store = store;
        _themeManager = themeManager;
        _settings = Normalize(loadResult.Settings);
        HasLoadDiagnostic = loadResult.DiagnosticMessage is not null;
        _lightTheme = IsLight(_settings.LightTheme) ? _settings.LightTheme : SongStashTheme.Peachy;
        _darkTheme = IsDark(_settings.DarkTheme) ? _settings.DarkTheme : SongStashTheme.Afterdark;
        _pet = _settings.Pet;
        _petAnimationLevel = _settings.PetAnimationLevel;
        _reduceAnimations = _settings.ReduceAnimations;
        _language = _settings.Language;
        ApplyLanguageCulture(_language);
        _launchAtStartup = _settings.LaunchAtStartup;
        _rememberWindowSizeAndPosition = _settings.RememberWindowSizeAndPosition;
        _startPage = _settings.StartPage;
        _notifyImportComplete = _settings.NotifyImportComplete;
        _notifyImportIssues = _settings.NotifyImportIssues;
        _confirmDestructiveActions = _settings.ConfirmDestructiveActions;
        _activeTheme = _themeManager.Apply(_settings);
        Backup = backup;
        if (Backup is not null)
        {
            Backup.SetSettingsSnapshot(_settings);
            Backup.WaitForSettingsSaveAsync = WaitForPendingSaveAsync;
            Backup.SettingsChanged += OnBackupSettingsChanged;
            Backup.SettingsRestored += ApplyRestoredSettings;
        }
        RefreshMusicServiceRows();
    }

    public bool HasLoadDiagnostic { get; }
    public SettingsStrings Strings => SettingsStrings.For(Language);
    public AppSettings CurrentSettings => _settings;
    public BackupViewModel? Backup { get; }
    public bool IsAppearanceTab => CurrentTab == SettingsTab.Appearance;
    public bool IsGeneralTab => CurrentTab == SettingsTab.General;
    public bool IsMusicServicesTab => CurrentTab == SettingsTab.MusicServices;
    public bool IsBackupTab => CurrentTab == SettingsTab.Backup;
    public bool IsPetVisible => Pet != SongStashPet.None;
    public string PetGlyph => Pet switch
    {
        SongStashPet.PinkCat => "🐱", SongStashPet.Salem => "🐈‍⬛", SongStashPet.Ghost => "👻",
        SongStashPet.Bat => "🦇", _ => string.Empty
    };
    public string PetLabel => Pet switch
    {
        SongStashPet.PinkCat => Strings.PinkCat, SongStashPet.Salem => Strings.Salem,
        SongStashPet.Ghost => Strings.Ghost, SongStashPet.Bat => Strings.Bat, _ => Strings.None
    };
    public bool IsPeachySelected => ActiveTheme == SongStashTheme.Peachy;
    public bool IsSageSelected => ActiveTheme == SongStashTheme.Sage;
    public bool IsMidnightSelected => ActiveTheme == SongStashTheme.Midnight;
    public bool IsAfterdarkSelected => ActiveTheme == SongStashTheme.Afterdark;
    public bool IsPinkCatSelected => Pet == SongStashPet.PinkCat;
    public bool IsSalemSelected => Pet == SongStashPet.Salem;
    public bool IsGhostSelected => Pet == SongStashPet.Ghost;
    public bool IsBatSelected => Pet == SongStashPet.Bat;
    public bool IsNoPetSelected => Pet == SongStashPet.None;
    public bool IsOverviewSelected => StartPage == SongStash.Application.Settings.StartPage.Overview;
    public bool IsLibrarySelected => StartPage == SongStash.Application.Settings.StartPage.Library;
    public bool HasSaveError
    {
        get => _hasSaveError;
        private set => SetProperty(ref _hasSaveError, value);
    }
    public IReadOnlyList<LanguageOption> LanguageOptions =>
    [
        new(AppLanguage.English, Strings.English),
        new(AppLanguage.Russian, Strings.Russian)
    ];
    public IReadOnlyList<string> LightThemeOptions => [Strings.Peachy, Strings.Sage];
    public IReadOnlyList<string> DarkThemeOptions => [Strings.Midnight, Strings.Afterdark];
    public IReadOnlyList<string> AnimationOptions => [Strings.Off, Strings.Subtle, Strings.Normal];
    public ObservableCollection<MusicServiceSettingsRowViewModel> MusicServiceRows { get; } = [];
    public IReadOnlyList<MusicServiceSettingsRowViewModel> MusicServiceOptions => MusicServiceRows.Where(row => row.IsEnabled).ToArray();
    public string? MusicServiceFeedback { get; private set; }
    public bool HasMusicServiceFeedback => !string.IsNullOrWhiteSpace(MusicServiceFeedback);
    public MusicService PreferredMusicService
    {
        get => Enum.Parse<MusicService>(_settings.PreferredMusicService);
        set
        {
            if (!Enum.IsDefined(value) || !_settings.EnabledMusicServices.Contains(value.ToString(), StringComparer.OrdinalIgnoreCase)) return;
            if (PreferredMusicService == value) return;
            _settings = _settings with { PreferredMusicService = value.ToString() };
            OnPropertyChanged(nameof(PreferredMusicService));
            foreach (var row in MusicServiceRows)
                row.SetPreferred(row.Service == value);
            Backup?.SetSettingsSnapshot(_settings);
            SetMusicServiceFeedback(null);
            _saveTask = PersistAsync();
            OnPropertyChanged(nameof(CurrentSettings));
        }
    }
    public SettingsTab CurrentTab
    {
        get => _currentTab;
        set
        {
            if (SetProperty(ref _currentTab, value))
            {
                OnPropertyChanged(nameof(IsAppearanceTab)); OnPropertyChanged(nameof(IsGeneralTab));
                OnPropertyChanged(nameof(IsMusicServicesTab)); OnPropertyChanged(nameof(IsBackupTab));
            }
        }
    }

    public SongStashTheme Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.FollowSystemTheme || !Enum.IsDefined(value) || _settings.Theme == value) return;
            Update(s => s with { Theme = value });
            OnPropertyChanged(nameof(Theme));
        }
    }
    public bool FollowSystemTheme
    {
        get => _settings.FollowSystemTheme;
        set
        {
            if (_settings.FollowSystemTheme == value) return;
            Update(s => s with { FollowSystemTheme = value });
            OnPropertyChanged(nameof(FollowSystemTheme));
        }
    }
    public SongStashTheme LightTheme
    {
        get => _lightTheme;
        set { if (!IsLight(value)) return; if (SetProperty(ref _lightTheme, value)) Update(s => s with { LightTheme = value }); }
    }
    public SongStashTheme DarkTheme
    {
        get => _darkTheme;
        set { if (!IsDark(value)) return; if (SetProperty(ref _darkTheme, value)) Update(s => s with { DarkTheme = value }); }
    }
    public SongStashPet Pet
    {
        get => _pet;
        set
        {
            if (!SetProperty(ref _pet, value)) return;
            Update(s => s with { Pet = value });
            OnPropertyChanged(nameof(IsPetVisible)); OnPropertyChanged(nameof(PetGlyph)); OnPropertyChanged(nameof(PetLabel));
            OnPropertyChanged(nameof(IsPinkCatSelected)); OnPropertyChanged(nameof(IsSalemSelected));
            OnPropertyChanged(nameof(IsGhostSelected)); OnPropertyChanged(nameof(IsBatSelected)); OnPropertyChanged(nameof(IsNoPetSelected));
        }
    }
    public PetAnimationLevel PetAnimationLevel
    {
        get => _petAnimationLevel;
        set { if (SetProperty(ref _petAnimationLevel, value)) Update(s => s with { PetAnimationLevel = value }); }
    }
    public bool ReduceAnimations
    {
        get => _reduceAnimations;
        set { if (SetProperty(ref _reduceAnimations, value)) Update(s => s with { ReduceAnimations = value }); }
    }
    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            ApplyLanguageCulture(value);
            if (!SetProperty(ref _language, value)) return;
            _settings = _settings with { Language = value };
            Backup?.SetSettingsSnapshot(_settings);
            OnPropertyChanged(nameof(CurrentSettings));
            _saveTask = PersistAsync();
            OnPropertyChanged(nameof(Strings)); OnPropertyChanged(nameof(PetLabel));
            OnPropertyChanged(nameof(LanguageOptions)); OnPropertyChanged(nameof(LightThemeOptions));
            OnPropertyChanged(nameof(DarkThemeOptions)); OnPropertyChanged(nameof(AnimationOptions));
            OnPropertyChanged(nameof(SelectedLanguageLabel)); OnPropertyChanged(nameof(SelectedPetAnimationLabel));
            OnPropertyChanged(nameof(SelectedLightThemeLabel)); OnPropertyChanged(nameof(SelectedDarkThemeLabel));
            OnPropertyChanged(nameof(SelectedStartPageLabel));
            RefreshMusicServiceRows();
        }
    }
    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set { if (SetProperty(ref _launchAtStartup, value)) Update(s => s with { LaunchAtStartup = value }); }
    }
    public bool RememberWindowSizeAndPosition
    {
        get => _rememberWindowSizeAndPosition;
        set { if (SetProperty(ref _rememberWindowSizeAndPosition, value)) Update(s => s with { RememberWindowSizeAndPosition = value }); }
    }
    public StartPage StartPage
    {
        get => _startPage;
        set { if (SetProperty(ref _startPage, value)) { Update(s => s with { StartPage = value }); OnPropertyChanged(nameof(SelectedStartPageLabel)); OnPropertyChanged(nameof(IsOverviewSelected)); OnPropertyChanged(nameof(IsLibrarySelected)); } }
    }
    public bool NotifyImportComplete
    {
        get => _notifyImportComplete;
        set { if (SetProperty(ref _notifyImportComplete, value)) Update(s => s with { NotifyImportComplete = value }); }
    }
    public bool NotifyImportIssues
    {
        get => _notifyImportIssues;
        set { if (SetProperty(ref _notifyImportIssues, value)) Update(s => s with { NotifyImportIssues = value }); }
    }
    public bool ConfirmDestructiveActions
    {
        get => _confirmDestructiveActions;
        set { if (SetProperty(ref _confirmDestructiveActions, value)) Update(s => s with { ConfirmDestructiveActions = value }); }
    }
    public SongStashTheme ActiveTheme => _activeTheme;
    public string SelectedLanguageLabel
    {
        get => Language == AppLanguage.English ? Strings.English : Strings.Russian;
        set
        {
            if (value == Strings.English) Language = AppLanguage.English;
            else if (value == Strings.Russian) Language = AppLanguage.Russian;
        }
    }
    public string SelectedLightThemeLabel
    {
        get => ThemeLabel(LightTheme);
        set { var theme = ThemeFromLabel(value); if (theme is SongStashTheme.Peachy or SongStashTheme.Sage) LightTheme = theme; }
    }
    public string SelectedDarkThemeLabel
    {
        get => ThemeLabel(DarkTheme);
        set { var theme = ThemeFromLabel(value); if (theme is SongStashTheme.Midnight or SongStashTheme.Afterdark) DarkTheme = theme; }
    }
    public string SelectedStartPageLabel
    {
        get => StartPage == SongStash.Application.Settings.StartPage.Overview ? Strings.Overview : Strings.AllSongs;
        set
        {
            if (value == Strings.Overview) StartPage = SongStash.Application.Settings.StartPage.Overview;
            else if (value == Strings.AllSongs) StartPage = SongStash.Application.Settings.StartPage.Library;
        }
    }
    public string SelectedPetAnimationLabel
    {
        get => PetAnimationLevel switch { PetAnimationLevel.Off => Strings.Off, PetAnimationLevel.Subtle => Strings.Subtle, _ => Strings.Normal };
        set
        {
            if (value == Strings.Off) PetAnimationLevel = PetAnimationLevel.Off;
            else if (value == Strings.Subtle) PetAnimationLevel = PetAnimationLevel.Subtle;
            else if (value == Strings.Normal) PetAnimationLevel = PetAnimationLevel.Normal;
        }
    }

    [RelayCommand] private void SelectTheme(SongStashTheme theme) => Theme = theme;
    [RelayCommand] private void SelectPet(SongStashPet pet) => Pet = pet;
    [RelayCommand] private void SelectTab(SettingsTab tab) => CurrentTab = tab;
    [RelayCommand] private void SelectStartPage(SongStash.Application.Settings.StartPage page) => StartPage = page;

    private void SetMusicServiceEnabled(MusicService service, bool enabled)
    {
        var name = service.ToString();
        var current = _settings.EnabledMusicServices.ToList();
        if (enabled && !current.Contains(name, StringComparer.OrdinalIgnoreCase)) current.Add(name);
        if (!enabled)
        {
            if (current.Count <= 1)
            {
                SetMusicServiceFeedback(Strings.AtLeastOneMusicService);
                RefreshMusicServiceRows();
                return;
            }
            if (PreferredMusicService == service)
            {
                SetMusicServiceFeedback(Strings.SelectPreferredFirst);
                RefreshMusicServiceRows();
                return;
            }
            current.RemoveAll(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        }
        _settings = _settings with { EnabledMusicServices = current };
        OnPropertyChanged(nameof(CurrentSettings));
        SetMusicServiceFeedback(null);
        RefreshMusicServiceRows();
        _saveTask = PersistAsync();
    }

    private void MoveMusicService(MusicService service, int delta)
    {
        var order = _settings.MusicServiceOrder.ToList();
        var index = order.FindIndex(value => string.Equals(value, service.ToString(), StringComparison.OrdinalIgnoreCase));
        MoveMusicServiceToIndex(service, index + delta);
    }

    public void MoveMusicServiceToIndex(MusicService service, int targetIndex)
    {
        var order = _settings.MusicServiceOrder.ToList();
        var index = order.FindIndex(value => string.Equals(value, service.ToString(), StringComparison.OrdinalIgnoreCase));
        if (index < 0 || targetIndex < 0 || targetIndex >= order.Count || index == targetIndex) return;
        order.RemoveAt(index);
        order.Insert(targetIndex, service.ToString());
        _settings = _settings with { MusicServiceOrder = order };
        OnPropertyChanged(nameof(CurrentSettings));
        RefreshMusicServiceRows();
        _saveTask = PersistAsync();
    }

    public void MoveMusicServiceToInsertionBoundary(MusicService service, int insertionBoundary)
    {
        var order = _settings.MusicServiceOrder.ToList();
        var sourceIndex = order.FindIndex(value => string.Equals(value, service.ToString(), StringComparison.OrdinalIgnoreCase));
        if (sourceIndex < 0 || insertionBoundary < 0 || insertionBoundary > order.Count) return;

        // The boundary is measured in the original list. Removing an item before
        // that boundary shifts its final index down by one.
        var targetIndex = insertionBoundary > sourceIndex ? insertionBoundary - 1 : insertionBoundary;
        MoveMusicServiceToIndex(service, targetIndex);
    }

    public void SetMusicServiceDragFeedback(MusicService? draggedService, int? insertionBoundary)
    {
        foreach (var row in MusicServiceRows)
        {
            row.IsDragging = draggedService == row.Service;
            row.IsDropBefore = false;
            row.IsDropAfter = false;
        }

        if (insertionBoundary is not int boundary || MusicServiceRows.Count == 0) return;
        if (boundary <= 0) MusicServiceRows[0].IsDropBefore = true;
        else if (boundary >= MusicServiceRows.Count) MusicServiceRows[^1].IsDropAfter = true;
        else MusicServiceRows[boundary].IsDropBefore = true;
    }

    private void RefreshMusicServiceRows()
    {
        MusicServiceRows.Clear();
        var enabled = _settings.EnabledMusicServices;
        var order = _settings.MusicServiceOrder;
        var services = order.Select(Enum.Parse<MusicService>).ToArray();
        for (var i = 0; i < services.Length; i++)
        {
            var service = services[i];
            MusicServiceRows.Add(new MusicServiceSettingsRowViewModel(service, MusicServiceLabel(service), Strings.Preferred,
                enabled.Contains(service.ToString(), StringComparer.OrdinalIgnoreCase),
                service == PreferredMusicService, i, services.Length, SetMusicServiceEnabled, MoveMusicService));
        }
        OnPropertyChanged(nameof(MusicServiceOptions));
    }

    private string MusicServiceLabel(MusicService service) => service switch
    {
        MusicService.YouTubeMusic => "YouTube Music", MusicService.YouTube => "YouTube",
        MusicService.Spotify => "Spotify", _ => "Apple Music"
    };

    private void SetMusicServiceFeedback(string? message)
    {
        MusicServiceFeedback = message;
        OnPropertyChanged(nameof(MusicServiceFeedback));
        OnPropertyChanged(nameof(HasMusicServiceFeedback));
    }

    public void ApplySystemThemeChange()
    {
        if (_settings.FollowSystemTheme) UpdateActiveTheme();
    }

    public Task WaitForPendingSaveAsync() => _saveTask;

    public async Task SaveWindowBoundsAsync(double width, double height, int? left, int? top)
    {
        if (!RememberWindowSizeAndPosition) return;
        _settings = _settings with { WindowWidth = width, WindowHeight = height, WindowLeft = left, WindowTop = top };
        Backup?.SetSettingsSnapshot(_settings);
        OnPropertyChanged(nameof(CurrentSettings));
        _saveTask = PersistAsync();
        await _saveTask;
    }

    private void Update(Func<AppSettings, AppSettings> update)
    {
        var previous = _settings;
        _settings = update(_settings);
        Backup?.SetSettingsSnapshot(_settings);
        OnPropertyChanged(nameof(CurrentSettings));
        if (previous.Theme != _settings.Theme || previous.FollowSystemTheme != _settings.FollowSystemTheme ||
            previous.LightTheme != _settings.LightTheme || previous.DarkTheme != _settings.DarkTheme)
            UpdateActiveTheme();
        _saveTask = PersistAsync();
    }

    private static void ApplyLanguageCulture(AppLanguage language)
    {
        var culture = CultureInfo.GetCultureInfo(language == AppLanguage.Russian ? "ru-RU" : "en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private void OnBackupSettingsChanged(AppSettings settings)
    {
        _settings = settings;
        Backup?.SetSettingsSnapshot(settings);
        OnPropertyChanged(nameof(CurrentSettings));
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(FollowSystemTheme));
        UpdateActiveTheme();
        _saveTask = PersistAsync();
    }

    private void ApplyRestoredSettings(AppSettings settings)
    {
        _settings = Normalize(settings);
        _lightTheme = _settings.LightTheme; _darkTheme = _settings.DarkTheme; _pet = _settings.Pet;
        _petAnimationLevel = _settings.PetAnimationLevel; _reduceAnimations = _settings.ReduceAnimations;
        _language = _settings.Language; _launchAtStartup = _settings.LaunchAtStartup;
        _rememberWindowSizeAndPosition = _settings.RememberWindowSizeAndPosition; _startPage = _settings.StartPage;
        _notifyImportComplete = _settings.NotifyImportComplete; _notifyImportIssues = _settings.NotifyImportIssues;
        _confirmDestructiveActions = _settings.ConfirmDestructiveActions;
        Backup?.ApplyRestoredSettings(_settings);
        UpdateActiveTheme(); RefreshMusicServiceRows();
        foreach (var property in new[] { nameof(CurrentSettings), nameof(Theme), nameof(FollowSystemTheme), nameof(LightTheme),
                     nameof(DarkTheme), nameof(Pet), nameof(PetAnimationLevel), nameof(ReduceAnimations), nameof(Language),
                     nameof(LaunchAtStartup), nameof(RememberWindowSizeAndPosition), nameof(StartPage), nameof(NotifyImportComplete),
                     nameof(NotifyImportIssues), nameof(ConfirmDestructiveActions), nameof(Strings), nameof(PetLabel), nameof(IsPetVisible),
                     nameof(SelectedLanguageLabel), nameof(SelectedStartPageLabel), nameof(SelectedPetAnimationLabel),
                     nameof(SelectedLightThemeLabel), nameof(SelectedDarkThemeLabel), nameof(ActiveTheme), nameof(IsPeachySelected),
                     nameof(IsSageSelected), nameof(IsMidnightSelected), nameof(IsAfterdarkSelected), nameof(IsPinkCatSelected),
                     nameof(IsSalemSelected), nameof(IsGhostSelected), nameof(IsBatSelected), nameof(IsNoPetSelected) })
            OnPropertyChanged(property);
    }

    private void UpdateActiveTheme()
    {
        _activeTheme = _themeManager.Apply(_settings);
        OnPropertyChanged(nameof(ActiveTheme));
        NotifyThemeSelection();
    }

    private async Task PersistAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            await _store.SaveAsync(_settings);
            HasSaveError = false;
        }
        catch (Exception)
        {
            HasSaveError = true;
        }
        finally { _saveLock.Release(); }
    }

    private string ThemeLabel(SongStashTheme theme) => theme switch
    {
        SongStashTheme.Sage => Strings.Sage, SongStashTheme.Midnight => Strings.Midnight,
        SongStashTheme.Afterdark => Strings.Afterdark, _ => Strings.Peachy
    };

    private SongStashTheme ThemeFromLabel(string? label) => label switch
    {
        var value when value == Strings.Sage => SongStashTheme.Sage,
        var value when value == Strings.Midnight => SongStashTheme.Midnight,
        var value when value == Strings.Afterdark => SongStashTheme.Afterdark,
        _ => SongStashTheme.Peachy
    };

    private static bool IsLight(SongStashTheme theme) => theme is SongStashTheme.Peachy or SongStashTheme.Sage;
    private static bool IsDark(SongStashTheme theme) => theme is SongStashTheme.Midnight or SongStashTheme.Afterdark;

    private static AppSettings Normalize(AppSettings settings)
    {
        var known = Enum.GetNames<MusicService>();
        var order = (settings.MusicServiceOrder ?? []).Where(value => known.Contains(value, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (order.Count == 0) order.AddRange(MusicServiceDefaults.Order);
        foreach (var name in MusicServiceDefaults.Order)
            if (!order.Contains(name, StringComparer.OrdinalIgnoreCase)) order.Add(name);
        var enabled = (settings.EnabledMusicServices ?? []).Where(value => known.Contains(value, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (enabled.Count == 0) enabled.Add(MusicServiceDefaults.Order[0]);
        var preferred = Enum.TryParse<MusicService>(settings.PreferredMusicService, true, out var requested) &&
                        enabled.Contains(requested.ToString(), StringComparer.OrdinalIgnoreCase)
            ? requested.ToString()
            : order.First(enabled.Contains);
        return settings with
    {
        Theme = Enum.IsDefined(settings.Theme) ? settings.Theme : SongStashTheme.Peachy,
        LightTheme = IsLight(settings.LightTheme) ? settings.LightTheme : SongStashTheme.Peachy,
        DarkTheme = IsDark(settings.DarkTheme) ? settings.DarkTheme : SongStashTheme.Afterdark,
        Pet = Enum.IsDefined(settings.Pet) ? settings.Pet : SongStashPet.PinkCat,
        PetAnimationLevel = Enum.IsDefined(settings.PetAnimationLevel) ? settings.PetAnimationLevel : PetAnimationLevel.Normal,
        Language = Enum.IsDefined(settings.Language) ? settings.Language : AppLanguage.English,
        StartPage = Enum.IsDefined(settings.StartPage) ? settings.StartPage : SongStash.Application.Settings.StartPage.Overview,
        AutomaticBackupFrequency = Enum.IsDefined(settings.AutomaticBackupFrequency) ? settings.AutomaticBackupFrequency : BackupFrequency.Off,
        PreferredMusicService = preferred,
        MusicServiceOrder = order,
        EnabledMusicServices = enabled
    };
    }

    private void NotifyThemeSelection()
    {
        OnPropertyChanged(nameof(IsPeachySelected)); OnPropertyChanged(nameof(IsSageSelected));
        OnPropertyChanged(nameof(IsMidnightSelected)); OnPropertyChanged(nameof(IsAfterdarkSelected));
    }
}
