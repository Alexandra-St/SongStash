using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SongStash.Desktop;

internal sealed class DatabaseRecoveryWindow : Window
{
    public DatabaseRecoveryWindow(Action retry, Action exit)
    {
        Title = "SongStash could not open your library";
        Width = 520;
        Height = 300;
        MinWidth = 460;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CanResize = false;

        var retryButton = new Button { Content = "Retry", MinWidth = 100 };
        retryButton.Click += (_, _) => retry();
        var exitButton = new Button { Content = "Close SongStash", MinWidth = 130 };
        exitButton.Click += (_, _) => exit();

        Content = new StackPanel
        {
            Margin = new Thickness(32),
            Spacing = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Your library could not be opened", FontSize = 22, FontWeight = FontWeight.SemiBold },
                new TextBlock
                {
                    Text = "SongStash has stopped before loading the library so your data is not replaced. Check that the library file is available, then retry. You can also close the app and restore from a backup after resolving the database issue.",
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 22
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { exitButton, retryButton }
                }
            }
        };
    }
}
