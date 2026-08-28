using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UnifiedMessenger.Services;

/// <summary>
/// Resolves a brush by resource key for an imperative (code-built) control.
///
/// Fluent NEUTRAL TEXT brushes (TextFillColorPrimary/Secondary/Tertiary/Disabled) must NOT be fetched from
/// <see cref="Application"/>.Current.Resources: that resolves the app-default theme, not the element's actual
/// theme, so on a light surface they can come back near-white and render invisibly (the light-mode command
/// centre / activity / reviews text bug). We build those explicitly from the element's <c>ActualTheme</c>
/// instead — black-on-light / white-on-dark at the fixed Fluent alphas. The same applies to the Fluent
/// CONTROL FILL brushes, for the same reason; this docstring used to exempt them ("control fills ... where
/// theme-invariance makes it safe") and that was simply wrong — see the switch below. What is left going to
/// the app resources is the semantic SystemFill* status colours and the accent, which genuinely do not flip
/// between themes.
/// </summary>
internal static class ThemeBrushResolver
{
    public static Brush Resolve(FrameworkElement element, string key)
    {
        switch (key)
        {
            case "TextFillColorPrimaryBrush": return Neutral(element, 0xE4, 0xFF);
            case "TextFillColorSecondaryBrush": return Neutral(element, 0x9E, 0xC5);
            case "TextFillColorTertiaryBrush": return Neutral(element, 0x72, 0x87);
            case "TextFillColorDisabledBrush": return Neutral(element, 0x5C, 0x5D);

            // CONTROL FILLS ARE NOT THEME-INVARIANT EITHER, and the comment above used to claim they were.
            // ControlSolidFillColorDefault is #FFFFFF on light and a dark grey on dark, so fetching it from
            // Application.Current.Resources hit exactly the failure this class exists to prevent — with one
            // extra twist that made it worse than the text case. BuildInsightStrip paired this background
            // with TextFillColorPrimaryBrush, which IS resolved correctly above: in dark theme the text came
            // back white (right) and the background came back white (wrong), so every per-account AI insight
            // rendered white-on-white. Observed on screen on a fresh dark launch: three account cards, each
            // showing a blank white bar with only the amber "AI" badge visible, on the dashboard's headline
            // feature. The Card* helpers below were added when the same thing happened to the Needs-reply
            // rows; the control fills were missed, so route them through the same themed surfaces.
            case "ControlSolidFillColorDefaultBrush": return CardBackground(element);
            case "ControlFillColorDefaultBrush": return CardBackground(element);
            case "ControlFillColorSecondaryBrush": return CardBackgroundSecondary(element);
        }

        return Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    /// <summary>
    /// True when <paramref name="element"/> is effectively on a dark theme. Prefers the element's own
    /// ActualTheme; if that isn't resolved yet (Default — common on a panel's first render before the theme
    /// propagates), it falls back to the WINDOW ROOT's theme (realised early, reliably the applied theme) and
    /// only then to the app/OS theme. Using the app/OS theme too eagerly was the bug: with a dark OS but a
    /// light in-app theme, a not-yet-themed panel drew white text on a light surface (invisible legend).
    /// </summary>
    public static bool IsDark(FrameworkElement element)
    {
        var theme = element.ActualTheme;
        if (theme == ElementTheme.Default &&
            UnifiedMessenger.App.CurrentWindow?.Content is FrameworkElement root &&
            root.ActualTheme != ElementTheme.Default)
        {
            theme = root.ActualTheme;
        }

        return theme switch
        {
            ElementTheme.Dark => true,
            ElementTheme.Light => false,
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark
        };
    }

    private static SolidColorBrush Neutral(FrameworkElement element, byte lightAlpha, byte darkAlpha) =>
        IsDark(element)
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(darkAlpha, 255, 255, 255))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(lightAlpha, 0, 0, 0));

    // ── Card surfaces for imperative (code-built) Borders/Buttons ────────────────────────────────
    // Built explicitly from ActualTheme (like the neutral text brushes) because the Fluent Card* brushes
    // fetched from Application.Resources can resolve the wrong theme and paint a light card in dark mode
    // (the Needs-reply rows). Solid, clean surfaces with a visible stroke in BOTH themes.

    /// <summary>A card surface: white on light, a subtle raised dark on dark.</summary>
    public static SolidColorBrush CardBackground(FrameworkElement element) => IsDark(element)
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2A, 0x2F, 0x38))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));

    /// <summary>A secondary/quieter card surface (e.g. chips, tiles).</summary>
    public static SolidColorBrush CardBackgroundSecondary(FrameworkElement element) => IsDark(element)
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x23, 0x28, 0x30))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA));

    /// <summary>A card outline visible on both light and dark grounds.</summary>
    public static SolidColorBrush CardStroke(FrameworkElement element) => IsDark(element)
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3A, 0x40, 0x4A))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xE1, 0xE7, 0xEE));
}
