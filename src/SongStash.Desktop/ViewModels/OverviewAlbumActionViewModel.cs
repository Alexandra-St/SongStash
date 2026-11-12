using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Library;

namespace SongStash.Desktop.ViewModels;

public sealed class OverviewAlbumActionViewModel(OverviewAlbumItem item, Action<Guid> select)
{
    public Guid SongId { get; } = item.SongId;
    public string Name { get; } = item.Name;
    public string Artist { get; } = item.Artist;
    public string SongTitle { get; } = item.SongTitle;
    public Uri? ArtworkUrl { get; } = item.ArtworkUrl;
    public string LatestFoundLabel { get; } = UserDateTimeFormatter.Format(item.LatestFoundAt);
    public RelayCommand SelectCommand { get; } = new(() => select(item.SongId));
}
