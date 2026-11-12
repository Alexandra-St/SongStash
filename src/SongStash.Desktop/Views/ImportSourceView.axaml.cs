using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SongStash.Desktop.ViewModels;

namespace SongStash.Desktop.Views;

public sealed partial class ImportSourceView : UserControl
{
    public ImportSourceView() => AvaloniaXamlLoader.Load(this);

    private async void ChooseFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ImportViewModel viewModel) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var selected = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = viewModel.Strings.ChooseFile,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Strings.FileTypeLabel) { Patterns = ["*.csv", "*.json"] }
            ]
        });

        var path = selected.Count == 0 ? null : selected[0].TryGetLocalPath();
        await viewModel.ReadFileAsync(path);
    }
}
