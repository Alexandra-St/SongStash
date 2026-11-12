using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Library;
using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed partial class OverviewViewModel : ObservableObject
{
    private readonly IOverviewQueryService query;
    private readonly SettingsViewModel? settings;

    public OverviewViewModel(IOverviewQueryService query, SettingsViewModel? settings = null)
    {
        this.query = query;
        this.settings = settings;
        if (settings is not null) settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public event Action<Guid>? SongSelected;
    public ObservableCollection<OverviewSongActionViewModel> RecentFinds { get; } = [];
    public ObservableCollection<OverviewSongActionViewModel> FavoriteSongs { get; } = [];
    public ObservableCollection<OverviewArtistItem> TopArtists { get; } = [];
    public ObservableCollection<OverviewGenreItem> TopGenres { get; } = [];
    public ObservableCollection<OverviewAlbumActionViewModel> RecentAlbums { get; } = [];

    [ObservableProperty] private int totalSongs;
    [ObservableProperty] private int totalFinds;
    [ObservableProperty] private int favoritesCount;
    [ObservableProperty] private int artistsCount;
    [ObservableProperty] private int genresCount;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isLoading;

    public OverviewStrings Strings => OverviewStrings.For(settings?.Language ?? AppLanguage.English);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmptyLibrary => !IsLoading && TotalSongs == 0;
    public bool HasRecentFinds => RecentFinds.Count > 0;
    public bool HasFavorites => FavoriteSongs.Count > 0;
    public bool HasTopArtists => TopArtists.Count > 0;
    public bool HasTopGenres => TopGenres.Count > 0;
    public bool HasRecentAlbums => RecentAlbums.Count > 0;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true; ErrorMessage = null;
        try
        {
            var model = await query.GetOverviewAsync(cancellationToken);
            TotalSongs = model.TotalSongs; TotalFinds = model.TotalFinds; FavoritesCount = model.FavoritesCount;
            ArtistsCount = model.ArtistsCount; GenresCount = model.GenresCount;
            Replace(RecentFinds, model.RecentFinds.Select(item => new OverviewSongActionViewModel(item,
                item.FoundAt is { } foundAt ? UserDateTimeFormatter.Format(foundAt) : Strings.DateUnknown, SelectSong)));
            Replace(FavoriteSongs, model.Favorites.Select(item => new OverviewSongActionViewModel(item, null, SelectSong)));
            Replace(TopArtists, model.TopArtists);
            Replace(TopGenres, model.TopGenres);
            Replace(RecentAlbums, model.RecentAlbums.Select(item => new OverviewAlbumActionViewModel(item, SelectSong)));
            NotifySections();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { ErrorMessage = Strings.LoadError; }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsEmptyLibrary)); }
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnTotalSongsChanged(int value) => OnPropertyChanged(nameof(IsEmptyLibrary));
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmptyLibrary));

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.Language)) return;
        OnPropertyChanged(nameof(Strings));
        _ = LoadAsync();
    }

    private void SelectSong(Guid id) => SongSelected?.Invoke(id);

    private void NotifySections()
    {
        OnPropertyChanged(nameof(HasRecentFinds)); OnPropertyChanged(nameof(HasFavorites));
        OnPropertyChanged(nameof(HasTopArtists)); OnPropertyChanged(nameof(HasTopGenres)); OnPropertyChanged(nameof(HasRecentAlbums));
        OnPropertyChanged(nameof(IsEmptyLibrary));
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values) collection.Add(value);
    }
}
