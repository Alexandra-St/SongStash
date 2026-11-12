using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Library;

namespace SongStash.Desktop.ViewModels;

public sealed class OverviewSongActionViewModel
{
    public OverviewSongActionViewModel(OverviewSongItem item, string? dateLabel, Action<Guid> select)
    {
        SongId = item.SongId; Title = item.Title; Artist = item.Artist; Album = item.Album;
        ArtworkUrl = item.ArtworkUrl; DateLabel = dateLabel;
        SelectCommand = new RelayCommand(() => select(SongId));
    }
    public Guid SongId { get; }
    public string Title { get; }
    public string Artist { get; }
    public string? Album { get; }
    public Uri? ArtworkUrl { get; }
    public string? DateLabel { get; }
    public RelayCommand SelectCommand { get; }
}
