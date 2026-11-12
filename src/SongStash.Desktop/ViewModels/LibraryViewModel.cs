using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Library;
using SongStash.Application.LibraryActions;
using SongStash.Application.Importing;
using SongStash.Application.MusicServices;
using SongStash.Application.Settings;
using SongStash.Desktop.Services;

namespace SongStash.Desktop.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryQueryService library;
    private readonly SettingsViewModel? settings;
    private readonly MusicServiceLinkResolver linkResolver;
    private readonly IExternalUriLauncher? uriLauncher;
    private readonly LibraryActionsService? actions;
    private Func<LibraryStrings, string>? errorText;

    public LibraryViewModel(ILibraryQueryService library, SettingsViewModel? settings = null,
        MusicServiceLinkResolver? linkResolver = null, IExternalUriLauncher? uriLauncher = null,
        ManualSongService? manualSongService = null, LibraryActionsService? actions = null, IClock? clock = null,
        ExportViewModel? export = null)
    {
        this.library = library;
        this.settings = settings;
        Details = new SongDetailsViewModel(() => Strings);
        selectedSortOption = SortOptions[0];
        this.linkResolver = linkResolver ?? new MusicServiceLinkResolver();
        this.uriLauncher = uriLauncher;
        this.actions = actions;
        Export = export;
        if (manualSongService is not null && clock is not null)
        {
            ManualAdd = new ManualAddViewModel(manualSongService, clock, settings);
            ManualAdd.SongSaved += OnManualSongSaved;
            ManualAdd.Cancelled += CloseManualAdd;
        }
        if (settings is not null) settings.PropertyChanged += OnSettingsPropertyChanged;
        RefreshMusicServiceActions();
    }

    public ObservableCollection<LibrarySongRowViewModel> Songs { get; } = [];
    public ObservableCollection<LibrarySongRowViewModel> FilteredSongs { get; } = [];
    public IReadOnlyList<LibrarySortOption> SortOptions =>
    [
        new(LibrarySortMode.RecentlyFound, Strings.RecentlyFound),
        new(LibrarySortMode.OldestFound, Strings.OldestFound),
        new(LibrarySortMode.TitleAscending, Strings.TitleAscending),
        new(LibrarySortMode.TitleDescending, Strings.TitleDescending),
        new(LibrarySortMode.ArtistAscending, Strings.ArtistAscending),
        new(LibrarySortMode.ArtistDescending, Strings.ArtistDescending)
    ];
    private LibrarySortOption selectedSortOption;
    public LibrarySortOption SelectedSortOption
    {
        get => selectedSortOption;
        set
        {
            if (value is null || !SortOptions.Any(option => option.Mode == value.Mode) || !SetProperty(ref selectedSortOption, value))
                return;
            ApplySearch();
        }
    }

    public ObservableCollection<MusicServiceActionViewModel> OtherMusicServiceActions { get; } = [];
    public SongDetailsViewModel Details { get; }
    public ManualAddViewModel? ManualAdd { get; }
    public ExportViewModel? Export { get; }
    public bool CanAddManually => ManualAdd is not null;
    public bool CanExport => Export is not null;

    [ObservableProperty] private bool isManualAddOpen;
    [ObservableProperty] private Guid? pendingDeleteSongId;
    [ObservableProperty] private Guid? pendingDeleteFindEventId;
    [ObservableProperty] private string? pendingDeleteMessage;

    public bool HasPendingDelete => PendingDeleteSongId.HasValue || PendingDeleteFindEventId.HasValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSongs))]
    [NotifyPropertyChangedFor(nameof(HasSongs))]
    private int songCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasNoSongs => SongCount == 0;
    public bool HasSongs => SongCount > 0;
    public bool HasNoResults => HasSongs && FilteredSongs.Count == 0;
    public bool HasSelection => selectedSongId.HasValue;
    public bool HasNoSelection => !HasSelection;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    [ObservableProperty] private string? openServiceError;
    public bool HasOpenServiceError => !string.IsNullOrWhiteSpace(OpenServiceError);
    public LibraryStrings Strings => LibraryStrings.For(settings?.Language ?? AppLanguage.English);
    public string SongCountLabel => Strings.SongCount(SongCount);
    public string PreferredServiceButtonText => Strings.OpenService(ServiceLabel(settings?.PreferredMusicService ?? MusicService.YouTubeMusic));
    public string MoreMusicServicesLabel => settings?.Strings.MoreMusicServices ?? "More…";
    public IReadOnlyList<MusicService> EnabledMusicServices
    {
        get
        {
            if (settings is null) return MusicServiceDefaults.Order.Select(Enum.Parse<MusicService>).ToArray();
            var enabled = settings.CurrentSettings.EnabledMusicServices;
            return settings.CurrentSettings.MusicServiceOrder
                .Select(value => Enum.TryParse<MusicService>(value, true, out var service) ? (MusicService?)service : null)
                .Where(service => service.HasValue && enabled.Contains(service.Value.ToString(), StringComparer.OrdinalIgnoreCase))
                .Select(service => service!.Value)
                .ToArray();
        }
    }
    private Guid? selectedSongId;
    private long selectionGeneration;

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(PreferredServiceButtonText));
        OnPropertyChanged(nameof(MoreMusicServicesLabel));
        OnPropertyChanged(nameof(EnabledMusicServices));
        if (e.PropertyName == nameof(SettingsViewModel.Language))
        {
            OnPropertyChanged(nameof(Strings));
            OnPropertyChanged(nameof(SongCountLabel));
            OnPropertyChanged(nameof(SortOptions));
            selectedSortOption = SortOptions.First(option => option.Mode == selectedSortOption.Mode);
            OnPropertyChanged(nameof(SelectedSortOption));
            foreach (var row in Songs) row.RefreshStrings();
            Details.RefreshStrings();
            if (PendingDeleteSongId.HasValue) PendingDeleteMessage = Strings.DeleteSongConfirmation;
            else if (PendingDeleteFindEventId.HasValue) PendingDeleteMessage = Strings.DeleteFindConfirmation;
            if (OpenServiceError is not null) OpenServiceError = Strings.OpenServiceFailed;
            if (errorText is not null) ErrorMessage = errorText(Strings);
        }
        RefreshMusicServiceActions();
    }

    private void SetErrorMessage(Func<LibraryStrings, string> getText)
    {
        errorText = getText;
        ErrorMessage = getText(Strings);
    }

    private void ClearErrorMessage()
    {
        errorText = null;
        ErrorMessage = null;
    }

    partial void OnSongCountChanged(int value) => OnPropertyChanged(nameof(SongCountLabel));

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ClearErrorMessage();
            var items = await library.GetSongsAsync(cancellationToken);
            Songs.Clear();
            foreach (var item in items)
            {
                Songs.Add(new LibrarySongRowViewModel(item, SelectSong, () => Strings));
            }

            SongCount = Songs.Count;
            ApplySearch();
            var selection = Songs.FirstOrDefault();
            if (selection is not null)
            {
                await SelectSongAsync(selection, cancellationToken);
            }
            else
            {
                ClearSelection();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SetErrorMessage(text => text.LoadFailed);
        }
    }

    public async Task SelectSongByIdAsync(Guid songId, CancellationToken cancellationToken = default)
    {
        if (Songs.Count == 0) await LoadAsync(cancellationToken);
        if (SearchText.Length > 0) SearchText = string.Empty;
        var item = Songs.FirstOrDefault(song => song.Id == songId);
        if (item is not null) await SelectSongAsync(item, cancellationToken);
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    partial void OnOpenServiceErrorChanged(string? value) => OnPropertyChanged(nameof(HasOpenServiceError));
    partial void OnPendingDeleteSongIdChanged(Guid? value) => OnPropertyChanged(nameof(HasPendingDelete));
    partial void OnPendingDeleteFindEventIdChanged(Guid? value) => OnPropertyChanged(nameof(HasPendingDelete));

    [RelayCommand]
    private void OpenManualAdd()
    {
        if (ManualAdd is null) return;
        ManualAdd.Reset();
        IsManualAddOpen = true;
    }

    private void CloseManualAdd() => IsManualAddOpen = false;

    public void ResetTransientState()
    {
        CloseManualAdd();
        CancelDelete();
    }

    private async Task OnManualSongSaved(Guid songId)
    {
        IsManualAddOpen = false;
        await LoadAsync();
        await SelectSongByIdAsync(songId);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (actions is null || !Details.HasSelection) return;
        var id = Details.Id;
        try
        {
            if (await actions.SetFavoriteAsync(id, !Details.IsFavorite))
                await ReloadAndSelectAsync(id);
        }
        catch { SetErrorMessage(text => text.FavoriteUpdateFailed); }
    }

    [RelayCommand]
    private void RequestDeleteSong()
    {
        if (!Details.HasSelection || actions is null) return;
        if (settings?.ConfirmDestructiveActions ?? true)
        {
            PendingDeleteSongId = Details.Id;
            PendingDeleteFindEventId = null;
            PendingDeleteMessage = Strings.DeleteSongConfirmation;
        }
        else _ = DeleteSongAsync(Details.Id);
    }

    public void RequestDeleteFindEvent(Guid findEventId)
    {
        if (actions is null || !Details.FindEvents.Any(item => item.Id == findEventId)) return;
        if (settings?.ConfirmDestructiveActions ?? true)
        {
            PendingDeleteFindEventId = findEventId;
            PendingDeleteSongId = null;
            PendingDeleteMessage = Strings.DeleteFindConfirmation;
        }
        else _ = DeleteFindEventAsync(findEventId);
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var songId = PendingDeleteSongId;
        var eventId = PendingDeleteFindEventId;
        CancelDelete();
        if (songId.HasValue) await DeleteSongAsync(songId.Value);
        else if (eventId.HasValue) await DeleteFindEventAsync(eventId.Value);
    }

    [RelayCommand]
    private void CancelDelete()
    {
        PendingDeleteSongId = null;
        PendingDeleteFindEventId = null;
        PendingDeleteMessage = null;
    }

    private async Task DeleteSongAsync(Guid id)
    {
        try
        {
            await actions!.DeleteSongAsync(id);
            ClearSelection();
            await LoadAsync();
            ClearSelection();
        }
        catch { SetErrorMessage(text => text.SongDeleteFailed); }
    }

    private async Task DeleteFindEventAsync(Guid id)
    {
        var songId = Details.Id;
        try
        {
            await actions!.DeleteFindEventAsync(id);
            await LoadAsync();
            await SelectSongByIdAsync(songId);
        }
        catch { SetErrorMessage(text => text.FindDeleteFailed); }
    }

    private async Task ReloadAndSelectAsync(Guid songId)
    {
        await LoadAsync();
        await SelectSongByIdAsync(songId);
    }

    private void ClearSelection()
    {
        selectionGeneration++;
        foreach (var song in Songs) song.IsSelected = false;
        selectedSongId = null;
        Details.Clear();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
    }

    [RelayCommand]
    private void OpenPreferredMusicService() => OpenMusicService(settings?.PreferredMusicService ?? MusicService.YouTubeMusic);

    [RelayCommand]
    private void OpenMusicService(MusicService service)
    {
        OpenServiceError = null;
        if (!Details.HasSelection || !EnabledMusicServices.Contains(service)) return;
        try
        {
            var uri = linkResolver.Resolve(Details.Title, Details.Artist, Details.SourceReferences, service);
            if (uriLauncher is null) throw new InvalidOperationException("No external link launcher is configured.");
            uriLauncher.Open(uri);
        }
        catch
        {
            OpenServiceError = Strings.OpenServiceFailed;
        }
    }

    private static string ServiceLabel(MusicService service) => service switch
    {
        MusicService.YouTubeMusic => "YouTube Music", MusicService.YouTube => "YouTube",
        MusicService.Spotify => "Spotify", _ => "Apple Music"
    };

    private void RefreshMusicServiceActions()
    {
        OtherMusicServiceActions.Clear();
        foreach (var service in EnabledMusicServices.Where(service => service != (settings?.PreferredMusicService ?? MusicService.YouTubeMusic)))
            OtherMusicServiceActions.Add(new MusicServiceActionViewModel(service, ServiceLabel(service), OpenMusicService));
    }

    private void ApplySearch()
    {
        var query = SearchText.Trim();
        FilteredSongs.Clear();
        var matches = Songs.Where(song =>
            query.Length == 0 || song.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            song.Artist.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var song in SortSongs(matches))
        {
            FilteredSongs.Add(song);
        }
        OnPropertyChanged(nameof(HasNoResults));
    }

    private IEnumerable<LibrarySongRowViewModel> SortSongs(IEnumerable<LibrarySongRowViewModel> songs) => SelectedSortOption.Mode switch
    {
        LibrarySortMode.OldestFound => songs
            .OrderBy(song => song.OldestFoundAt.HasValue ? 0 : 1)
            .ThenBy(song => song.OldestFoundAt)
            .ThenBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.Artist, StringComparer.CurrentCultureIgnoreCase),
        LibrarySortMode.TitleAscending => songs
            .OrderBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.Artist, StringComparer.CurrentCultureIgnoreCase),
        LibrarySortMode.TitleDescending => songs
            .OrderByDescending(song => song.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.Artist, StringComparer.CurrentCultureIgnoreCase),
        LibrarySortMode.ArtistAscending => songs
            .OrderBy(song => string.IsNullOrWhiteSpace(song.Artist) ? 1 : 0)
            .ThenBy(song => string.IsNullOrWhiteSpace(song.Artist) ? string.Empty : song.Artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase),
        LibrarySortMode.ArtistDescending => songs
            .OrderBy(song => string.IsNullOrWhiteSpace(song.Artist) ? 1 : 0)
            .ThenByDescending(song => string.IsNullOrWhiteSpace(song.Artist) ? string.Empty : song.Artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase),
        _ => songs
            .OrderByDescending(song => song.LatestFoundAt.HasValue)
            .ThenByDescending(song => song.LatestFoundAt)
            .ThenBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(song => song.Artist, StringComparer.CurrentCultureIgnoreCase)
    };

    private void SelectSong(LibrarySongRowViewModel item) => _ = SelectSongAsync(item);

    private async Task SelectSongAsync(LibrarySongRowViewModel item, CancellationToken cancellationToken = default)
    {
        try
        {
            ClearErrorMessage();
            var generation = ++selectionGeneration;
            Details.Clear();
            foreach (var song in Songs)
            {
                song.IsSelected = song.Id == item.Id;
            }

            selectedSongId = item.Id;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
            var details = await library.GetSongDetailsAsync(item.Id, cancellationToken);
            if (details is not null && selectedSongId == item.Id && selectionGeneration == generation)
            {
                Details.Load(details, RequestDeleteFindEvent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SetErrorMessage(text => text.DetailsLoadFailed);
        }
    }
}

public enum LibrarySortMode
{
    RecentlyFound,
    OldestFound,
    TitleAscending,
    TitleDescending,
    ArtistAscending,
    ArtistDescending
}

public sealed record LibrarySortOption(LibrarySortMode Mode, string Label);
