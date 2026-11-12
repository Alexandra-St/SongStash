using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using SongStash.Application.Settings;
using SongStash.Desktop.ViewModels;

namespace SongStash.Desktop.Views;

public sealed partial class SettingsView : UserControl
{
    private IPointer? _reorderPointer;
    private MusicService? _draggedService;
    private Point _pointerStart;
    private bool _dragStarted;
    private ItemsControl? _musicServiceItems;

    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
        _musicServiceItems = this.FindControl<ItemsControl>("MusicServiceItems");
        AddHandler(InputElement.PointerPressedEvent, OnReorderHandlePointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, OnReorderHandlePointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, OnReorderHandlePointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.PointerCaptureLostEvent, OnReorderHandlePointerCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.KeyDownEvent, OnReorderHandleKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is SettingsViewModel { Backup: { } backup }) await backup.LoadAsync();
        };
    }

    private async void OnChooseBackupFile(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || DataContext is not SettingsViewModel { Backup: { } backup }) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("SongStash backup") { Patterns = ["*.songstashbackup"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) await backup.InspectAsync(path);
    }

    private async void OnChooseBackupFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || DataContext is not SettingsViewModel { Backup: { } backup }) return;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) await backup.ChooseFolderAsync(path);
    }

    private void OnReorderHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (FindReorderHandle(e.Source as Visual) is not Button handle ||
            !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed ||
            handle.DataContext is not MusicServiceSettingsRowViewModel row)
            return;

        _reorderPointer = e.Pointer;
        _draggedService = row.Service;
        _pointerStart = e.GetPosition(this);
        _dragStarted = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnReorderHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_reorderPointer != e.Pointer || _draggedService is not MusicService service ||
            DataContext is not SettingsViewModel settings)
            return;

        var position = e.GetPosition(this);
        if (!_dragStarted && Math.Abs(position.X - _pointerStart.X) < 4 && Math.Abs(position.Y - _pointerStart.Y) < 4)
            return;

        _dragStarted = true;
        var items = _musicServiceItems;
        var boundary = items is null || TopLevel.GetTopLevel(items) is null ? null : GetInsertionBoundary(e.GetPosition(items).Y);
        settings.SetMusicServiceDragFeedback(service, boundary);
        e.Handled = true;
    }

    private void OnReorderHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_reorderPointer != e.Pointer) return;

        if (_dragStarted && _draggedService is MusicService service && DataContext is SettingsViewModel settings)
        {
            var items = _musicServiceItems;
            var boundary = items is null || TopLevel.GetTopLevel(items) is null ? null : GetInsertionBoundary(e.GetPosition(items).Y);
            if (boundary is int insertAt)
            {
                settings.MoveMusicServiceToInsertionBoundary(service, insertAt);
            }
            settings.SetMusicServiceDragFeedback(null, null);
        }

        ResetPointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnReorderHandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_reorderPointer is not null && DataContext is SettingsViewModel settings)
            settings.SetMusicServiceDragFeedback(null, null);
        _reorderPointer = null;
        _draggedService = null;
        _dragStarted = false;
    }

    private void OnReorderHandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _dragStarted)
        {
            if (DataContext is SettingsViewModel settings) settings.SetMusicServiceDragFeedback(null, null);
            if (_reorderPointer is not null) ResetPointerCapture(_reorderPointer);
            e.Handled = true;
            return;
        }

        if ((e.KeyModifiers & KeyModifiers.Alt) == 0 ||
            FindReorderHandle(e.Source as Visual)?.DataContext is not MusicServiceSettingsRowViewModel row)
            return;

        if (e.Key == Key.Up) row.MoveUpCommand.Execute(null);
        else if (e.Key == Key.Down) row.MoveDownCommand.Execute(null);
        else return;
        e.Handled = true;
    }

    private int? GetInsertionBoundary(double pointerY)
    {
        return ResolveInsertionBoundary(_musicServiceItems, pointerY);
    }

    public static int? ResolveInsertionBoundary(ItemsControl? itemsControl, double pointerY)
    {
        if (itemsControl is null || TopLevel.GetTopLevel(itemsControl) is null) return null;

        var listHeight = itemsControl.Bounds.Height;
        var count = itemsControl.ItemCount;
        if (!double.IsFinite(pointerY) || !double.IsFinite(listHeight) || listHeight <= 0 ||
            pointerY < 0 || pointerY > listHeight || count <= 0)
            return null;

        for (var index = 0; index < count; index++)
        {
            if (itemsControl.ContainerFromIndex(index) is not Control container || TopLevel.GetTopLevel(container) is null)
                return null;

            var rowBounds = container.Bounds;
            if (!double.IsFinite(rowBounds.Height) || rowBounds.Height <= 0)
                return null;

            var origin = container.TranslatePoint(new Point(0, 0), itemsControl);
            if (origin is not Point point || !double.IsFinite(point.Y))
                return null;

            if (pointerY < point.Y + rowBounds.Height / 2)
                return index;
        }

        return count;
    }

    private static Button? FindReorderHandle(Visual? visual)
    {
        while (visual is not null)
        {
            if (visual is Button button && button.Classes.Contains("music-service-drag-handle"))
                return button;
            visual = visual.GetVisualParent();
        }

        return null;
    }

    private void ResetPointerCapture(IPointer pointer)
    {
        _reorderPointer = null;
        _draggedService = null;
        _dragStarted = false;
        pointer.Capture(null);
    }
}
