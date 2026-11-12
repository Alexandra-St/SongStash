using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed class MusicServiceActionViewModel(MusicService service, string label, Action<MusicService> open)
{
    public MusicService Service { get; } = service;
    public string Label { get; } = label;
    public RelayCommand OpenCommand { get; } = new(() => open(service));
}
