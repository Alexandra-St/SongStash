using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SongStash.Application.Settings;

namespace SongStash.Desktop.Services;

public static class ThemePalette
{
    private static readonly IReadOnlyDictionary<string, string> Peachy = new Dictionary<string, string>
    {
        ["WindowBackgroundBrush"] = "#FFF8F0EC", ["SidebarBrush"] = "#FFFCF6F3", ["SurfaceBrush"] = "#FFFCF6F3", ["SurfaceSecondaryBrush"] = "#FFF5E9E8", ["SurfaceHoverBrush"] = "#FFFFF0EB",
        ["SelectionBrush"] = "#FFFFE5E2", ["AccentBrush"] = "#FFDA5874", ["AccentHoverBrush"] = "#FFC64D69", ["FocusRingBrush"] = "#FFDA5874",
        ["TextOnAccentBrush"] = "#FFFFFFFF", ["ScrollBarBackgroundBrush"] = "#00000000",
        ["ScrollBarThumbBrush"] = "#FFC8B6BD", ["ScrollBarThumbHoverBrush"] = "#FFA98591",
        ["ScrollBarTrackBrush"] = "#18DA5874", ["TextPrimaryBrush"] = "#FF29212B",
        ["TextSecondaryBrush"] = "#FF766D78", ["BorderBrush"] = "#FFE9D9D7", ["PlaceholderBrush"] = "#FFF5E9E8",
        ["StatusNewBackgroundBrush"] = "#FFE2F3E8", ["StatusNewTextBrush"] = "#FF226A45",
        ["StatusLibraryBackgroundBrush"] = "#FFE5EDFF", ["StatusLibraryTextBrush"] = "#FF3156A5",
        ["StatusWarningBackgroundBrush"] = "#FFFFEBD8", ["StatusWarningTextBrush"] = "#FF9A5612",
        ["StatusReviewBackgroundBrush"] = "#FFEFE6FF", ["StatusReviewTextBrush"] = "#FF6743A5",
        ["StatusErrorBackgroundBrush"] = "#FFFFE5E2", ["StatusErrorTextBrush"] = "#FFA52F3D"
    };

    private static readonly IReadOnlyDictionary<string, string> Sage = new Dictionary<string, string>
    {
        ["WindowBackgroundBrush"] = "#FFF0F4EB", ["SidebarBrush"] = "#FFF6F8F1", ["SurfaceBrush"] = "#FFF6F8F1", ["SurfaceSecondaryBrush"] = "#FFE8EEE6", ["SurfaceHoverBrush"] = "#FFEEF3EB",
        ["SelectionBrush"] = "#FFDDEBDD", ["AccentBrush"] = "#FF668D73", ["AccentHoverBrush"] = "#FF567D63", ["FocusRingBrush"] = "#FF668D73",
        ["TextOnAccentBrush"] = "#FFFFFFFF", ["ScrollBarBackgroundBrush"] = "#00000000",
        ["ScrollBarThumbBrush"] = "#FFB7C9BA", ["ScrollBarThumbHoverBrush"] = "#FF849C89",
        ["ScrollBarTrackBrush"] = "#18668D73", ["TextPrimaryBrush"] = "#FF24332A",
        ["TextSecondaryBrush"] = "#FF64746A", ["BorderBrush"] = "#FFD9E2D9", ["PlaceholderBrush"] = "#FFE8EEE6",
        ["StatusNewBackgroundBrush"] = "#FFDCEEE2", ["StatusNewTextBrush"] = "#FF245C3A",
        ["StatusLibraryBackgroundBrush"] = "#FFE1EAF7", ["StatusLibraryTextBrush"] = "#FF34567F",
        ["StatusWarningBackgroundBrush"] = "#FFF8EBD7", ["StatusWarningTextBrush"] = "#FF8A561D",
        ["StatusReviewBackgroundBrush"] = "#FFEEE7F5", ["StatusReviewTextBrush"] = "#FF644779",
        ["StatusErrorBackgroundBrush"] = "#FFF8E3E0", ["StatusErrorTextBrush"] = "#FF9A3540"
    };

    private static readonly IReadOnlyDictionary<string, string> Midnight = new Dictionary<string, string>
    {
        ["WindowBackgroundBrush"] = "#FF101824", ["SidebarBrush"] = "#FF172333", ["SurfaceBrush"] = "#FF172333", ["SurfaceSecondaryBrush"] = "#FF24364C", ["SurfaceHoverBrush"] = "#FF213249",
        ["SelectionBrush"] = "#FF263D5B", ["AccentBrush"] = "#FF69A9F4", ["AccentHoverBrush"] = "#FF8DC0FF", ["FocusRingBrush"] = "#FF69A9F4",
        ["TextOnAccentBrush"] = "#FF101824", ["ScrollBarBackgroundBrush"] = "#00000000",
        ["ScrollBarThumbBrush"] = "#FF526B85", ["ScrollBarThumbHoverBrush"] = "#FF7999BB",
        ["ScrollBarTrackBrush"] = "#4069A9F4", ["TextPrimaryBrush"] = "#FFEAF2FE",
        ["TextSecondaryBrush"] = "#FFADC0D5", ["BorderBrush"] = "#FF31445A", ["PlaceholderBrush"] = "#FF24364C",
        ["StatusNewBackgroundBrush"] = "#FF1D4135", ["StatusNewTextBrush"] = "#FF9CE0B5",
        ["StatusLibraryBackgroundBrush"] = "#FF203D62", ["StatusLibraryTextBrush"] = "#FFA8C9FF",
        ["StatusWarningBackgroundBrush"] = "#FF513A25", ["StatusWarningTextBrush"] = "#FFFFCD8C",
        ["StatusReviewBackgroundBrush"] = "#FF3C3157", ["StatusReviewTextBrush"] = "#FFD0B4FF",
        ["StatusErrorBackgroundBrush"] = "#FF542C35", ["StatusErrorTextBrush"] = "#FFFFA6AD"
    };

    private static readonly IReadOnlyDictionary<string, string> Afterdark = new Dictionary<string, string>
    {
        ["WindowBackgroundBrush"] = "#FF121118", ["SidebarBrush"] = "#FF1B1922", ["SurfaceBrush"] = "#FF1B1922", ["SurfaceSecondaryBrush"] = "#FF2A2633", ["SurfaceHoverBrush"] = "#FF27232E",
        ["SelectionBrush"] = "#FF352A42", ["AccentBrush"] = "#FFB18AF4", ["AccentHoverBrush"] = "#FFC5AAFF", ["FocusRingBrush"] = "#FFB18AF4",
        ["TextOnAccentBrush"] = "#FF121118", ["ScrollBarBackgroundBrush"] = "#00000000",
        ["ScrollBarThumbBrush"] = "#FF625B6D", ["ScrollBarThumbHoverBrush"] = "#FF8E7BAB",
        ["ScrollBarTrackBrush"] = "#40B18AF4", ["TextPrimaryBrush"] = "#FFF2EDF8",
        ["TextSecondaryBrush"] = "#FFB6AFC1", ["BorderBrush"] = "#FF393441", ["PlaceholderBrush"] = "#FF2A2633",
        ["StatusNewBackgroundBrush"] = "#FF203B31", ["StatusNewTextBrush"] = "#FF9CE0B5",
        ["StatusLibraryBackgroundBrush"] = "#FF263750", ["StatusLibraryTextBrush"] = "#FFA8C9FF",
        ["StatusWarningBackgroundBrush"] = "#FF4D3825", ["StatusWarningTextBrush"] = "#FFFFCD8C",
        ["StatusReviewBackgroundBrush"] = "#FF3B2E51", ["StatusReviewTextBrush"] = "#FFD0B4FF",
        ["StatusErrorBackgroundBrush"] = "#FF4A2931", ["StatusErrorTextBrush"] = "#FFFFA6AD"
    };

    public static bool IsDark(SongStashTheme theme) => theme is SongStashTheme.Midnight or SongStashTheme.Afterdark;

    public static SongStashTheme Resolve(AppSettings settings, bool systemIsDark) => settings.FollowSystemTheme
        ? systemIsDark ? settings.DarkTheme : settings.LightTheme
        : settings.Theme;

    public static void Apply(IResourceDictionary resources, SongStashTheme theme)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var palette = theme switch
        {
            SongStashTheme.Sage => Sage,
            SongStashTheme.Midnight => Midnight,
            SongStashTheme.Afterdark => Afterdark,
            _ => Peachy
        };
        foreach (var (key, color) in palette)
            resources[key] = new SolidColorBrush(Color.Parse(color));

        // Fluent TextBox templates resolve their focused, hover and selection
        // colors from these keys; regular TextBox styles lose to the template's
        // nested state selectors for those parts.
        var textBoxResources = new Dictionary<string, string>
        {
            ["TextControlForeground"] = palette["TextPrimaryBrush"],
            ["TextControlForegroundPointerOver"] = palette["TextPrimaryBrush"],
            ["TextControlForegroundFocused"] = palette["TextPrimaryBrush"],
            ["TextControlForegroundDisabled"] = palette["TextSecondaryBrush"],
            ["TextControlBackground"] = palette["SurfaceBrush"],
            ["TextControlBackgroundPointerOver"] = palette["SurfaceBrush"],
            ["TextControlBackgroundFocused"] = palette["SurfaceBrush"],
            ["TextControlBackgroundDisabled"] = palette["SurfaceSecondaryBrush"],
            ["TextControlBorderBrush"] = palette["BorderBrush"],
            ["TextControlBorderBrushPointerOver"] = palette["AccentHoverBrush"],
            ["TextControlBorderBrushFocused"] = palette["FocusRingBrush"],
            ["TextControlBorderBrushDisabled"] = palette["BorderBrush"],
            ["TextControlSelectionHighlightColor"] = palette["SelectionBrush"],
            ["TextControlPlaceholderForeground"] = palette["TextSecondaryBrush"]
        };
        foreach (var (key, color) in textBoxResources)
            resources[key] = new SolidColorBrush(Color.Parse(color));
        resources["TextControlBorderThemeThicknessFocused"] = new Thickness(2);

        // Fluent ToggleSwitch's ControlTheme reads these dynamic resource keys
        // from its template and nested pointer/disabled state selectors. Styling
        // the template parts from Application.Styles loses to those ControlTheme
        // selectors, so supply the actual palette keys consumed by the template.
        var accent = palette["AccentBrush"];
        var accentHover = palette["AccentHoverBrush"];
        var surface = palette["SurfaceHoverBrush"];
        var border = palette["BorderBrush"];
        var text = palette["TextPrimaryBrush"];
        var secondaryText = palette["TextSecondaryBrush"];
        var onAccent = palette["TextOnAccentBrush"];
        var toggleResources = new Dictionary<string, string>
        {
            ["ToggleSwitchFillOff"] = surface,
            ["ToggleSwitchStrokeOff"] = border,
            ["ToggleSwitchKnobFillOff"] = text,
            ["ToggleSwitchFillOn"] = accent,
            ["ToggleSwitchStrokeOn"] = accent,
            ["ToggleSwitchKnobFillOn"] = onAccent,
            ["ToggleSwitchFillOffPointerOver"] = surface,
            ["ToggleSwitchStrokeOffPointerOver"] = accentHover,
            ["ToggleSwitchKnobFillOffPointerOver"] = text,
            ["ToggleSwitchFillOnPointerOver"] = accentHover,
            ["ToggleSwitchStrokeOnPointerOver"] = accentHover,
            ["ToggleSwitchKnobFillOnPointerOver"] = onAccent,
            ["ToggleSwitchFillOffPressed"] = surface,
            ["ToggleSwitchStrokeOffPressed"] = accent,
            ["ToggleSwitchKnobFillOffPressed"] = text,
            ["ToggleSwitchFillOnPressed"] = accentHover,
            ["ToggleSwitchStrokeOnPressed"] = accentHover,
            ["ToggleSwitchKnobFillOnPressed"] = onAccent,
            ["ToggleSwitchFillOffDisabled"] = surface,
            ["ToggleSwitchStrokeOffDisabled"] = border,
            ["ToggleSwitchKnobFillOffDisabled"] = secondaryText,
            ["ToggleSwitchFillOnDisabled"] = surface,
            ["ToggleSwitchStrokeOnDisabled"] = border,
            ["ToggleSwitchKnobFillOnDisabled"] = secondaryText
        };
        foreach (var (key, color) in toggleResources)
            resources[key] = new SolidColorBrush(Color.Parse(color));
    }
}
