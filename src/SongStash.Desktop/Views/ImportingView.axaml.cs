using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SongStash.Desktop.Views;

public sealed partial class ImportingView : UserControl
{
    public ImportingView() => AvaloniaXamlLoader.Load(this);
}
