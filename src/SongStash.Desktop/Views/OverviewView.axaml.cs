using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SongStash.Desktop.Views;

public sealed partial class OverviewView : UserControl
{
    public OverviewView() => AvaloniaXamlLoader.Load(this);
}
