using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Settings;

namespace SongStash.Desktop.ViewModels;

public sealed partial class MusicServiceSettingsRowViewModel : ObservableObject
{
    private readonly Action<MusicService, bool> _setEnabled;
    private readonly Action<MusicService, int> _move;
    private bool _isPreferred;

    public MusicServiceSettingsRowViewModel(MusicService service, string displayName, string preferredLabel, bool enabled,
        bool preferred, int index, int count, Action<MusicService, bool> setEnabled, Action<MusicService, int> move)
    {
        Service = service;
        DisplayName = displayName;
        PreferredLabel = preferredLabel;
        isEnabled = enabled;
        _isPreferred = preferred;
        Index = index;
        Count = count;
        _setEnabled = setEnabled;
        _move = move;
    }

    public MusicService Service { get; }
    public string DisplayName { get; }
    public string PreferredLabel { get; }
    public int Index { get; }
    public int Count { get; }
    public bool IsPreferred
    {
        get => _isPreferred;
        private set => SetProperty(ref _isPreferred, value);
    }
    public bool CanMoveUp => Index > 0;
    public bool CanMoveDown => Index < Count - 1;
    public double DragOpacity => IsDragging ? 0.62 : 1;

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private bool isDragging;

    [ObservableProperty]
    private bool isDropBefore;

    [ObservableProperty]
    private bool isDropAfter;

    partial void OnIsDraggingChanged(bool value) => OnPropertyChanged(nameof(DragOpacity));

    partial void OnIsEnabledChanged(bool value) => _setEnabled(Service, value);

    public void SetPreferred(bool value) => IsPreferred = value;

    [RelayCommand] private void MoveUp() => _move(Service, -1);
    [RelayCommand] private void MoveDown() => _move(Service, 1);
}
