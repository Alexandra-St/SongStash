using CommunityToolkit.Mvvm.Input;
using SongStash.Domain.Songs;

namespace SongStash.Desktop.ViewModels;

public sealed class ManualSongMatchOptionViewModel(Song song, string attachLabel, Func<Guid, Task> attach)
{
    public Guid Id { get; } = song.Id;
    public string Title { get; } = song.Title;
    public string Artist { get; } = song.Artist;
    public string AttachLabel { get; } = attachLabel;
    public AsyncRelayCommand AttachCommand { get; } = new(async () => await attach(song.Id));
}
