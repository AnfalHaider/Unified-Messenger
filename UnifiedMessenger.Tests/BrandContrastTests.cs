using System.Globalization;
using System.Text.RegularExpressions;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-A11Y-02 — WCAG 2.1 contrast for the brand token, computed from the shipping theme files.
///
/// <para>
/// These read <c>Themes/Tokens.xaml</c> directly rather than hard-coding the hex values, so changing the
/// brand colour without checking contrast fails the build instead of shipping. That matters here: the
/// token has already changed once (from #14B8A6 to #1B75BB), and <c>docs/design-system/contrast-audit.md</c>
/// was left describing the old colour — so the documented failure and the real failure were in different
/// themes, and nothing caught it.
/// </para>
/// <para>
/// The brand brush is used for the message-volume chart line and stroke, the empty-state icon, and the
/// accent button's background. The chart line and icon are graphical objects essential to understanding
/// the view, so WCAG 1.4.11 applies at 3:1; treating them as text-grade at 4.5:1 is the stricter bar and
/// is what these tests require.
/// </para>
/// </summary>
public class BrandContrastTests
{
    // Surfaces the brand brush actually sits on, taken from the WinUI defaults the app uses.
    private const string LightCard = "#FFFFFF";
    private const string DarkCard = "#2D2D30";
    private const string DarkChrome = "#1E1E1E";

    private const double AaText = 4.5;
    private const double AaLargeOrUi = 3.0;

    private static string TokensXaml =>
        File.ReadAllText(Path.Combine(RepoRoot(), "UnifiedMessenger", "Themes", "Tokens.xaml"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnifiedMessenger.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>Pulls a colour out of a named theme dictionary block in Tokens.xaml.</summary>
    private static string BrandColorForTheme(string themeKey)
    {
        var xaml = TokensXaml;
        var themeStart = xaml.IndexOf($"x:Key=\"{themeKey}\"", StringComparison.Ordinal);
        Assert.True(themeStart >= 0, $"no '{themeKey}' theme dictionary found in Tokens.xaml");

        var block = xaml[themeStart..];
        var end = block.IndexOf("</ResourceDictionary>", StringComparison.Ordinal);
        block = end > 0 ? block[..end] : block;

        var match = Regex.Match(block, @"x:Key=""UmBrandTealColor"">(#[0-9A-Fa-f]{6})<");
        Assert.True(match.Success, $"UmBrandTealColor is not defined inside the '{themeKey}' theme dictionary");
        return match.Groups[1].Value;
    }

    private static double RelativeLuminance(string hex)
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

    private static double Contrast(string a, string b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        (l1, l2) = l1 >= l2 ? (l1, l2) : (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }

    [Fact]
    public void BrandIsDefinedSeparatelyForLightAndDark()
    {
        // A single brand value cannot satisfy both themes; this pins that the split exists at all.
        Assert.NotEqual(BrandColorForTheme("Light"), BrandColorForTheme("Default"));
    }

    [Fact]
    public void BrandOnLightCardMeetsTextContrast()
    {
        var ratio = Contrast(BrandColorForTheme("Light"), LightCard);

        Assert.True(ratio >= AaText, $"brand on light card is {ratio:F2}:1, needs {AaText}:1");
    }

    [Fact]
    public void BrandOnDarkCardMeetsTextContrast()
    {
        // THE regression. #1B75BB measured 2.82:1 here — failing even the 3:1 non-text bar — while the
        // chart line and empty-state icon were drawn in it.
        var ratio = Contrast(BrandColorForTheme("Default"), DarkCard);

        Assert.True(ratio >= AaText, $"brand on dark card is {ratio:F2}:1, needs {AaText}:1");
    }

    [Fact]
    public void BrandOnDarkChromeMeetsTextContrast()
    {
        var ratio = Contrast(BrandColorForTheme("Default"), DarkChrome);

        Assert.True(ratio >= AaText, $"brand on dark chrome is {ratio:F2}:1, needs {AaText}:1");
    }

    [Theory]
    [InlineData("Light", "#FFFFFF")]   // light theme: TextOnAccentFillColorPrimary is white
    [InlineData("Default", "#000000")] // dark theme: it is near-black
    public void AccentButtonTextMeetsContrastOnTheBrandBackground(string theme, string foreground)
    {
        // AccentButtonStyle paints the brand as a BACKGROUND, so lightening it for dark theme had to be
        // checked from this direction too — the Re-sync button uses this style.
        var ratio = Contrast(foreground, BrandColorForTheme(theme));

        Assert.True(ratio >= AaText, $"accent button text in {theme} is {ratio:F2}:1, needs {AaText}:1");
    }

    [Fact]
    public void GraphicalUseClearsTheNonTextBarWithMargin()
    {
        // WCAG 1.4.11 floor for the chart line / icon, asserted separately so the intent survives even if
        // someone later argues the text-grade bar is too strict for a graphic.
        Assert.True(Contrast(BrandColorForTheme("Default"), DarkCard) >= AaLargeOrUi);
        Assert.True(Contrast(BrandColorForTheme("Light"), LightCard) >= AaLargeOrUi);
    }
}
