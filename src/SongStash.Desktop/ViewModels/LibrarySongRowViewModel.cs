using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Library;

namespace SongStash.Desktop.ViewModels;

public sealed partial class LibrarySongRowViewModel : ObservableObject
{
    private readonly LibrarySongItem song;
    private readonly Func<LibraryStrings> strings;

    public LibrarySongRowViewModel(LibrarySongItem song, Action<LibrarySongRowViewModel> select,
        Func<LibraryStrings>? strings = null)
    {
        this.song = song;
        this.select = select;
        this.strings = strings ?? (() => LibraryStrings.For(SongStash.Application.Settings.AppLanguage.English));
        isFavorite = song.IsFavorite;
    }

    private readonly Action<LibrarySongRowViewModel> select;
    public Guid Id => song.Id;
    public string Title => song.Title;
    public string Artist => song.Artist;
    public string? Album => song.Album;
    public Uri? ArtworkUrl => song.ArtworkUrl;
    public int FindCount => song.FindCount;
    public DateTimeOffset? LatestFoundAt => song.LatestFoundAt;
    public DateTimeOffset? OldestFoundAt => song.OldestFoundAt;
    public string FindCountText => song.FindCount switch
    {
        0 => strings().NotFoundYet,
        1 => strings().FoundOnce,
        _ => strings().FoundTimes(song.FindCount)
    };

    public void RefreshStrings() => OnPropertyChanged(nameof(FindCountText));

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private bool isSelected;

    [RelayCommand]
    private void Select() => select(this);
}
