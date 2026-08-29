using System.Globalization;
using System.Text.RegularExpressions;

namespace UnifiedMessenger.Tests;

/// <summary>
/// WCAG 2.1 relative-luminance and contrast maths, plus a reader for the shipping theme files.
///
/// <para>
/// Extracted from <see cref="BrandContrastTests"/>, where it was written for the brand token, so the
/// semantic status colours can be measured with the same arithmetic instead of a second copy of it. The
/// point of reading <c>Themes/Tokens.xaml</c> rather than hard-coding hex values is that changing a colour
/// without checking contrast then fails the build — the brand token had already changed once with the
/// audit doc left describing the old value, and nothing caught it.
/// </para>
/// </summary>
internal static class WcagContrast
{
    /// <summary>WCAG 1.4.3 AA for normal-size text.</summary>
    public const double AaText = 4.5;

    /// <summary>WCAG 1.4.3 AA for large text, and 1.4.11 for UI components and graphical objects.</summary>
    public const double AaLargeOrUi = 3.0;

    // The surfaces these colours are actually drawn on.
    //
    // These were hard-coded to "#FFFFFF" / "#2D2D30" / "#1E1E1E" and described as "the WinUI defaults the
    // app uses". That was true when written and stopped being true when the app gained its own surface
    // tokens: it ships #17191D / #121418 / #0E0F12 on dark, and neither #2D2D30 nor #1E1E1E appears
    // anywhere in Tokens.xaml. So this file measured the right foregrounds against backgrounds that are
    // never drawn, and — because only ONE light surface was listed — never measured the light sunken
    // surface at all. That is where the two real failures were hiding (see
    // EveryStatusColourIsReadableOnEverySurfaceOfItsOwnTheme).
    //
    // Reading them from Tokens.xaml is the same rule the colours already follow, and for the same reason
    // the docstring above gives: changing one without re-checking contrast should fail the build.
    public static string LightCard => ThemeColor("Light", "UmSurfaceColor");
    public static string DarkCard => ThemeColor("Default", "UmSurfaceColor");
    public static string DarkChrome => ThemeColor("Default", "UmCanvasColor");

    /// <summary>
    /// Every surface a card or its text can sit on, for the given theme dictionary ("Light" / "Default").
    /// </summary>
    /// <remarks>
    /// Contrast has to hold on the worst of these, not on a representative one. A status colour is drawn
    /// inside cards that sit on all three, and picking one surface to test is how the sunken surface went
    /// unmeasured for the life of the palette.
    /// </remarks>
    public static (string Name, string Hex)[] Surfaces(string themeKey) =>
    [
        ("surface", ThemeColor(themeKey, "UmSurfaceColor")),
        ("sunken", ThemeColor(themeKey, "UmSurfaceSunkenColor")),
        ("canvas", ThemeColor(themeKey, "UmCanvasColor"))
    ];

    /// <summary>
    /// The colour actually rendered when <paramref name="foreground"/> is drawn at
    /// <paramref name="alpha"/> over <paramref name="background"/>.
    /// </summary>
    /// <remarks>
    /// WinUI's <c>Opacity</c> composites the element against whatever is behind it. Measuring the
    /// undimmed token therefore measures a pixel that is never drawn — the token can be compliant while
    /// the rendered text is not, and nothing in this file could previously tell the two apart.
    /// </remarks>
    public static string Composite(string foreground, string background, double alpha)
    {
        static int Channel(string hex, int offset) => int.Parse(
            hex.TrimStart('#').Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var blended = new[] { 0, 2, 4 }.Select(offset =>
        {
            var value = Channel(foreground, offset) * alpha + Channel(background, offset) * (1 - alpha);
            return ((int)Math.Round(value)).ToString("X2", CultureInfo.InvariantCulture);
        });

        return "#" + string.Concat(blended);
    }

    /// <summary>Contrast of <paramref name="foreground"/> drawn at <paramref name="alpha"/> over
    /// <paramref name="background"/>, against that same background.</summary>
    public static double RatioAtOpacity(string foreground, string background, double alpha) =>
        Ratio(Composite(foreground, background, alpha), background);

    /// <summary>
    /// The opacity values <c>Tokens.xaml</c> declares for dimmed text, read from the file.
    /// </summary>
    public static double OpacityToken(string key)
    {
        var match = Regex.Match(TokensXaml(), $@"x:Key=""{Regex.Escape(key)}"">([0-9.]+)<");
        Assert.True(match.Success, $"'{key}' is not declared in Tokens.xaml");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnifiedMessenger.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    public static string TokensXaml() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "UnifiedMessenger", "Themes", "Tokens.xaml"));

    /// <summary>
    /// Reads a colour defined at the dictionary's top level — outside the theme dictionaries — so it is
    /// the single value both themes share.
    /// </summary>
    public static string SharedColor(string key)
    {
        var xaml = TokensXaml();
        var themeBlockEnd = xaml.IndexOf("</ResourceDictionary.ThemeDictionaries>", StringComparison.Ordinal);
        Assert.True(themeBlockEnd > 0, "Tokens.xaml has no ThemeDictionaries block");

        var shared = xaml[themeBlockEnd..];
        var match = Regex.Match(shared, $@"x:Key=""{Regex.Escape(key)}"">(#[0-9A-Fa-f]{{6}})<");
        Assert.True(match.Success, $"'{key}' is not defined outside the theme dictionaries in Tokens.xaml");
        return match.Groups[1].Value;
    }

    /// <summary>Reads a colour from a named theme dictionary ("Light" or "Default").</summary>
    public static string ThemeColor(string themeKey, string colorKey)
    {
        var xaml = TokensXaml();
        var themeStart = xaml.IndexOf($"x:Key=\"{themeKey}\"", StringComparison.Ordinal);
        Assert.True(themeStart >= 0, $"no '{themeKey}' theme dictionary found in Tokens.xaml");

        var block = xaml[themeStart..];
        var end = block.IndexOf("</ResourceDictionary>", StringComparison.Ordinal);
        block = end > 0 ? block[..end] : block;

        var match = Regex.Match(block, $@"x:Key=""{Regex.Escape(colorKey)}"">(#[0-9A-Fa-f]{{6}})<");
        Assert.True(match.Success, $"'{colorKey}' is not defined inside the '{themeKey}' theme dictionary");
        return match.Groups[1].Value;
    }

    /// <summary>True when <paramref name="colorKey"/> has its own value in both theme dictionaries.</summary>
    public static bool IsThemed(string colorKey)
    {
        var xaml = TokensXaml();
        var themeBlockEnd = xaml.IndexOf("</ResourceDictionary.ThemeDictionaries>", StringComparison.Ordinal);
        if (themeBlockEnd <= 0)
        {
            return false;
        }

        var themes = xaml[..themeBlockEnd];
        return Regex.Matches(themes, $@"x:Key=""{Regex.Escape(colorKey)}""").Count >= 2;
    }

    public static double RelativeLuminance(string hex)
    {
        hex = hex.TrimStart('#');
        var channels = new[]
        {
            int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
            int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
            int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0
        };

        static double Linearise(double c) =>
            c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        return 0.2126 * Linearise(channels[0])
             + 0.7152 * Linearise(channels[1])
             + 0.0722 * Linearise(channels[2]);
    }

    public static double Ratio(string a, string b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        (l1, l2) = l1 >= l2 ? (l1, l2) : (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }
}
