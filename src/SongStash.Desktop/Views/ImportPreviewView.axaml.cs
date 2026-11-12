using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SongStash.Desktop.Views;

public sealed partial class ImportPreviewView : UserControl
{
    public ImportPreviewView() => AvaloniaXamlLoader.Load(this);
}
