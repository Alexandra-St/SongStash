using CommunityToolkit.Mvvm.ComponentModel;
using SongStash.Application.Library;
using SongStash.Domain.ExternalReferences;

namespace SongStash.Desktop.ViewModels;

public sealed partial class SongDetailsViewModel : ObservableObject
{
    private readonly Func<LibraryStrings> strings;

    public SongDetailsViewModel(Func<LibraryStrings>? strings = null) =>
        this.strings = strings ?? (() => LibraryStrings.For(SongStash.Application.Settings.AppLanguage.English));

    [ObservableProperty] private Guid id;
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string artist = string.Empty;
    [ObservableProperty] private string? album;
    [ObservableProperty] private Uri? artworkUrl;
    [ObservableProperty] private string? genre;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private IReadOnlyList<FindEventViewModel> findEvents = [];
    [ObservableProperty] private IReadOnlyList<ExternalReferenceViewModel> externalReferences = [];
    [ObservableProperty] private IReadOnlyList<ExternalReference> sourceReferences = [];

    public bool HasSelection => Id != Guid.Empty;
    public bool HasNoSelection => !HasSelection;
    public bool HasExternalReferences => ExternalReferences.Count > 0;
    public string FavoriteActionLabel => IsFavorite ? strings().RemoveFromFavorites : strings().AddToFavorites;
    public string GenreText => string.IsNullOrWhiteSpace(Genre) ? string.Empty : string.Format(strings().GenreFormat, Genre);
    public string FindCountLabel => strings().FindCount(FindEvents.Count);

    public void RefreshStrings()
    {
        OnPropertyChanged(nameof(FavoriteActionLabel));
        OnPropertyChanged(nameof(GenreText));
        OnPropertyChanged(nameof(FindCountLabel));
        foreach (var item in FindEvents) item.RefreshStrings();
    }

    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteActionLabel));
    partial void OnGenreChanged(string? value) => OnPropertyChanged(nameof(GenreText));

    public void Clear()
    {
        Id = Guid.Empty;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
        Title = Artist = string.Empty;
        Album = null; ArtworkUrl = null; Genre = null; Notes = null;
        IsFavorite = false;
        FindEvents = [];
        OnPropertyChanged(nameof(FindCountLabel));
        ExternalReferences = [];
        SourceReferences = [];
        OnPropertyChanged(nameof(HasExternalReferences));
    }

    public void Load(SongDetails details, Action<Guid>? requestDeleteFindEvent = null)
    {
        Id = details.Id;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
        Title = details.Title;
        Artist = details.Artist;
        Album = details.Album;
        ArtworkUrl = details.ArtworkUrl;
        Genre = details.Genre;
        Notes = details.Notes;
        IsFavorite = details.IsFavorite;
        FindEvents = details.FindEvents.Select(item => new FindEventViewModel(item, requestDeleteFindEvent, strings)).ToArray();
        OnPropertyChanged(nameof(FindCountLabel));
        ExternalReferences = details.ExternalReferences.Select(item => new ExternalReferenceViewModel(item)).ToArray();
        SourceReferences = details.ExternalReferences;
        OnPropertyChanged(nameof(HasExternalReferences));
    }
}
