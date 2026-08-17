using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UnifiedMessenger.Services;

/// <summary>
/// Semantic brushes resolved from <c>Themes/Tokens.xaml</c> resource keys.
///
/// <para>
/// <b>The status colours are resolved in code, not looked up.</b> They used to be one shared value for
/// both themes, which is not a preference but an impossibility: to clear WCAG AA text contrast a colour
/// needs relative luminance ≤ 0.183 against a white card and ≥ 0.294 against the dark card, and those
/// ranges do not overlap. So they had to become per-theme — and the moment they did, the old
/// <c>Application.Current.Resources.TryGetValue</c> lookup stopped being correct, for the reason
/// <see cref="ThemeBrushResolver"/> already documents: that call resolves the <i>app</i> theme, not the
/// element's, and its comment explicitly excused itself on the grounds that these values were
/// theme-invariant. They no longer are.
/// </para>
/// <para>
/// The palette is therefore duplicated between here and Tokens.xaml — here for code-built controls, there
/// for XAML consumers — and <c>StatusContrastTests</c> asserts the two agree, so the copy cannot drift.
/// </para>
/// </summary>
public static class UmSemanticBrushes
{
    public const string StatusSuccessBrushKey = "UmStatusSuccessBrush";
    public const string StatusWarningBrushKey = "UmStatusWarningBrush";
    public const string StatusDangerBrushKey = "UmStatusDangerBrush";
    public const string StatusInfoBrushKey = "UmStatusInfoBrush";
    public const string StatusNeutralBrushKey = "UmStatusNeutralBrush";
    public const string StatusMutedBrushKey = "UmStatusMutedBrush";
    public const string TransparentBrushKey = "UmTransparentBrush";

    /// <summary>
    /// Light/dark hex for each status brush. Must match the per-theme values in Tokens.xaml — pinned by
    /// test. Measured as text: light 5.02 / 5.02 / 4.83 on a white card; dark 6.03 / 6.39 / 4.96 on the
    /// dark card.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (string Light, string Dark)> StatusPalette =
        new Dictionary<string, (string Light, string Dark)>(StringComparer.Ordinal)
        {
            [StatusSuccessBrushKey] = ("#15803D", "#22C55E"),
            [StatusWarningBrushKey] = ("#B45309", "#F59E0B"),
            [StatusDangerBrushKey] = ("#DC2626", "#F87171"),
            // The three the sidebar's connection dot needs. They must be in this table as well as in the
            // theme dictionary: a key missing here falls through to grey, which is how a status signal
            // silently stops signalling.
            [StatusInfoBrushKey] = ("#1D4ED8", "#60A5FA"),
            [StatusNeutralBrushKey] = ("#5B6773", "#94A3B8"),
            [StatusMutedBrushKey] = ("#6B7684", "#8A97A6")
        };

    public static SolidColorBrush StatusSuccess => Get(StatusSuccessBrushKey);

    public static SolidColorBrush StatusWarning => Get(StatusWarningBrushKey);

    public static SolidColorBrush StatusDanger => Get(StatusDangerBrushKey);

    public static SolidColorBrush StatusInfo => Get(StatusInfoBrushKey);

    public static SolidColorBrush StatusNeutral => Get(StatusNeutralBrushKey);

    public static SolidColorBrush StatusMuted => Get(StatusMutedBrushKey);

    public static SolidColorBrush Transparent => Get(TransparentBrushKey);

    /// <summary>
    /// Resolves a semantic brush. Pass <paramref name="element"/> where one is available so the status
    /// colours follow that element's actual theme rather than the app default — the same distinction that
    /// caused the light-mode invisible-text bug for the neutral brushes.
    /// </summary>
    public static SolidColorBrush Get(string resourceKey, FrameworkElement? element = null)
    {
        if (StatusPalette.TryGetValue(resourceKey, out var palette))
        {
            var dark = element is not null
                ? ThemeBrushResolver.IsDark(element)
                : Application.Current?.RequestedTheme == ApplicationTheme.Dark;

            return new SolidColorBrush(ParseHex(dark ? palette.Dark : palette.Light));
        }

        if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true &&
            resource is SolidColorBrush brush)
        {
            return brush;
        }

        // Was a silent fallback to grey. A status colour quietly becoming grey is exactly the kind of
        // invisible failure this audit keeps turning up, so it now leaves a trace.
        AppLogger.LogWarning("Theme", $"Semantic brush '{resourceKey}' did not resolve; falling back to grey.");
        return new SolidColorBrush(Colors.Gray);
    }

    internal static Windows.UI.Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(
            255,
            byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
