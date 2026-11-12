using Avalonia;
using Avalonia.Styling;
using SongStash.Application.Settings;

namespace SongStash.Desktop.Services;

public sealed class ThemeManager
{
    private readonly Func<bool> _isSystemDark;

    public ThemeManager(Func<bool>? isSystemDark = null) =>
        _isSystemDark = isSystemDark ?? IsCurrentSystemDark;

    private static bool IsCurrentSystemDark() =>
        Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public SongStashTheme Apply(AppSettings settings)
    {
        var application = Avalonia.Application.Current;
        if (application is not null && settings.FollowSystemTheme)
            application.RequestedThemeVariant = ThemeVariant.Default;

        var theme = ThemePalette.Resolve(settings, _isSystemDark());
        if (application is null) return theme;

        if (!settings.FollowSystemTheme)
            application.RequestedThemeVariant = ThemePalette.IsDark(theme) ? ThemeVariant.Dark : ThemeVariant.Light;
        ThemePalette.Apply(application.Resources, theme);
        return theme;
    }
}
