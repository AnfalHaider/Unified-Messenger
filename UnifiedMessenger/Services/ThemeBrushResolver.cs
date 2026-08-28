using System.Collections.Generic;
using System.Linq;
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

        // ANY key declared in a ThemeDictionary has the same problem as the Fluent brushes above, and that
        // includes the app's OWN tokens. Tokens.xaml declares UmSurfaceBrush, UmHairlineBrush,
        // UmAccentWashBrush and the rest inside <ResourceDictionary.ThemeDictionaries>, so a plain
        // Application.Current.Resources lookup returns the app-default theme's value — white, in dark mode.
        //
        // XAML consumers are fine: {ThemeResource UmSurfaceBrush} resolves per element. Imperative callers
        // going through Brush("UmSurfaceBrush") were not, and the Reviews desk is built imperatively, so in
        // dark theme it drew white cards, white filter pills and six blank white tiles with text that had
        // correctly resolved to white on top of them. Observed on screen.
        //
        // Resolve from the element's own theme dictionary first. This covers every themed token at once
        // rather than key by key, which is what the switch above had been reduced to doing.
        var themed = FromThemeDictionary(element, key);
        if (themed is not null)
        {
            return themed;
        }

        return Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    /// <summary>
    /// For static builders that have no element to hand — resolves against the window root's theme.
    /// </summary>
    /// <remarks>
    /// Correct because the root's <c>ActualTheme</c> is the applied theme, and every surface these builders
    /// produce is parented into that same window. Prefer the element overload wherever an element exists;
    /// this is for helpers like <c>WeeklyReportDialog.Populate</c>, which is static and shared between the
    /// dialog and the Reports page.
    /// </remarks>
    public static Brush Resolve(string key) =>
        UnifiedMessenger.App.CurrentWindow?.Content is FrameworkElement root
            ? Resolve(root, key)
            : Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
                ? brush
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    /// <summary>
    /// Looks <paramref name="key"/> up in the theme dictionary matching <paramref name="element"/>'s actual
    /// theme, searching the application dictionary and everything merged into it.
    /// </summary>
    /// <remarks>
    /// Returns null when high contrast is active. <see cref="ThemeService"/> installs HighContrast.xaml as a
    /// merged dictionary, which wins the ordinary lookup precisely because it is merged last — reaching past
    /// it into a Light/Default theme dictionary would repaint the high-contrast surfaces with ordinary
    /// colours, which is the one case where the "wrong" lookup was doing the right thing.
    /// </remarks>
    private static Brush? FromThemeDictionary(FrameworkElement element, string key)
    {
        if (HighContrastOverridesInstalled())
        {
            return null;
        }

        var themeKey = IsDark(element) ? "Default" : "Light";

        foreach (var dictionary in Flatten(Application.Current.Resources))
        {
            if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var themedObject) &&
                themedObject is ResourceDictionary themed &&
                themed.TryGetValue(key, out var value) &&
                value is Brush brush)
            {
                return brush;
            }
        }

        return null;
    }

    private static bool HighContrastOverridesInstalled() =>
        Application.Current.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Contains("HighContrast", System.StringComparison.OrdinalIgnoreCase) == true);

    private static IEnumerable<ResourceDictionary> Flatten(ResourceDictionary root)
    {
        yield return root;

        foreach (var merged in root.MergedDictionaries)
        {
            foreach (var nested in Flatten(merged))
            {
                yield return nested;
            }
        }
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
