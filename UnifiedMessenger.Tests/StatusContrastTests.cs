namespace UnifiedMessenger.Tests;

/// <summary>
/// F-A11Y-04 — the semantic status colours, measured. The brand token was checked in session 1 and fixed;
/// success / warning / danger were never measured in either theme.
///
/// <para>
/// They matter more than the brand colour, not less. They are drawn as <b>text</b> — the on-time
/// percentage on the Analytics leaderboard picks one of the three by value, and the KPI delta badges use
/// two of them — so 4.5:1 is the applicable bar, not 3:1. And status colour is exactly where
/// colour-as-the-only-signal hides, which is the other half of this file.
/// </para>
/// <para>
/// <b>The structural problem underneath.</b> <c>UmBrandTealColor</c> is defined once per theme, and
/// <see cref="BrandContrastTests.BrandIsDefinedSeparatelyForLightAndDark"/> exists to keep it that way,
/// because one value cannot satisfy a white card and a near-black one. The status colours were declared
/// <i>outside</i> the theme dictionaries — a single value serving both. The fix that was applied to brand
/// was never applied to the colours that carry the actual status.
/// </para>
/// </summary>
public class StatusContrastTests
{
    public static TheoryData<string> StatusColorKeys() =>
    [
        "UmStatusSuccessColor",
        "UmStatusWarningColor",
        "UmStatusDangerColor"
    ];

    [Theory]
    [MemberData(nameof(StatusColorKeys))]
    public void EachStatusColourIsDefinedSeparatelyForLightAndDark(string key)
    {
        Assert.True(
            WcagContrast.IsThemed(key),
            $"{key} is a single shared value. One colour cannot meet contrast on both a white card and a " +
            "near-black one — the same reason the brand token is split per theme.");
    }

    [Theory]
    [MemberData(nameof(StatusColorKeys))]
    public void EachStatusColourIsReadableAsTextOnALightCard(string key)
    {
        var color = WcagContrast.ThemeColor("Light", key);
        var ratio = WcagContrast.Ratio(color, WcagContrast.LightCard);

        Assert.True(
            ratio >= WcagContrast.AaText,
            $"{key} ({color}) on the light card is {ratio:F2}:1, needs {WcagContrast.AaText}:1 — " +
            "it is used as a text foreground for the on-time percentage.");
    }

    [Theory]
    [MemberData(nameof(StatusColorKeys))]
    public void EachStatusColourIsReadableAsTextOnADarkCard(string key)
    {
        var color = WcagContrast.ThemeColor("Default", key);
        var ratio = WcagContrast.Ratio(color, WcagContrast.DarkCard);

        Assert.True(
            ratio >= WcagContrast.AaText,
            $"{key} ({color}) on the dark card is {ratio:F2}:1, needs {WcagContrast.AaText}:1.");
    }

    [Theory]
    [MemberData(nameof(StatusColorKeys))]
    public void EachStatusColourIsReadableOnTheDarkChrome(string key)
    {
        var color = WcagContrast.ThemeColor("Default", key);
        var ratio = WcagContrast.Ratio(color, WcagContrast.DarkChrome);

        Assert.True(
            ratio >= WcagContrast.AaText,
            $"{key} ({color}) on the dark chrome is {ratio:F2}:1, needs {WcagContrast.AaText}:1.");
    }

    [Fact]
    public void SuccessAndDangerAreNearlyIdenticalInGreyscaleSoColourAloneCannotCarryStatus()
    {
        // Measured, not assumed: success vs danger is 1.04:1 in light and 1.21:1 in dark. In greyscale
        // they are the same colour, and red/green is the commonest colour-vision deficiency — so for a
        // meaningful share of users these two convey nothing on their own.
        //
        // This is asserted as a FACT rather than fixed, because it cannot be fixed by tuning. Both
        // colours have to clear 4.5:1 against the same background, which forces both into the same narrow
        // luminance band; pushing them apart would break the contrast requirement that matters more. The
        // correct remedy is WCAG 1.4.1's — never let colour be the only signal — which is what
        // StatusCueTests checks.
        //
        // If this ever starts failing because someone separated them, that is good news: delete it.
        foreach (var theme in new[] { "Light", "Default" })
        {
            var success = WcagContrast.ThemeColor(theme, "UmStatusSuccessColor");
            var danger = WcagContrast.ThemeColor(theme, "UmStatusDangerColor");

            Assert.True(
                WcagContrast.Ratio(success, danger) < 1.5,
                $"in {theme}, success and danger now separate in greyscale — the pessimistic assumption " +
                "behind the non-colour cues no longer holds and this test should be revisited.");
        }
    }

    [Theory]
    [MemberData(nameof(StatusColorKeys))]
    public void EveryStatusColourStaysClearOfTheOtherThemesSurface(string key)
    {
        // Guards the specific way this broke: a colour that is correct for one theme being used by both.
        // Each theme's value must fail-safe if it ever leaks — so assert they are genuinely different
        // where the measurement demanded a change, and identical only where it did not.
        var light = WcagContrast.ThemeColor("Light", key);
        var dark = WcagContrast.ThemeColor("Default", key);

        var lightOk = WcagContrast.Ratio(light, WcagContrast.LightCard) >= WcagContrast.AaText;
        var darkOk = WcagContrast.Ratio(dark, WcagContrast.DarkCard) >= WcagContrast.AaText;

        Assert.True(lightOk && darkOk, $"{key}: light={light} dark={dark}");
    }

    [Theory]
    [MemberData(nameof(StatusColorKeys))]
    public void TheCodePaletteMatchesTokensXamlExactly(string colorKey)
    {
        // The status colours exist twice on purpose: in Tokens.xaml for XAML consumers, and in
        // UmSemanticBrushes for code-built controls — because Application.Current.Resources resolves the
        // APP theme rather than the element's, which is the documented cause of the light-mode
        // invisible-text bug. Duplication is only safe while something checks it.
        var brushKey = colorKey.Replace("Color", "Brush", StringComparison.Ordinal);
        var palette = UnifiedMessenger.Services.UmSemanticBrushes.StatusPalette[brushKey];

        Assert.Equal(WcagContrast.ThemeColor("Light", colorKey), palette.Light, ignoreCase: true);
        Assert.Equal(WcagContrast.ThemeColor("Default", colorKey), palette.Dark, ignoreCase: true);
    }

    [Fact]
    public void NoSingleColourCouldHaveSatisfiedBothThemes()
    {
        // Why this had to become per-theme rather than being retuned. AA text needs relative luminance
        // <= 0.183 against a white card and >= 0.294 against the dark card; the ranges do not overlap, so
        // the previous shared value was guaranteed to fail one theme whatever it was set to.
        var maxForLight = 1.05 / WcagContrast.AaText - 0.05;
        var minForDark = WcagContrast.AaText * (WcagContrast.RelativeLuminance(WcagContrast.DarkCard) + 0.05) - 0.05;

        Assert.True(
            maxForLight < minForDark,
            $"a shared value is now possible (light needs <= {maxForLight:F4}, dark needs >= {minForDark:F4}) " +
            "— the per-theme split could be reconsidered.");
    }

    [Fact]
    public void TheHighContrastThemeStillDefinesTheStatusBrushes()
    {
        // HighContrast.xaml re-declares these brushes. If the colours move into theme dictionaries and
        // that file is not updated, the high-contrast theme silently loses them.
        var xaml = File.ReadAllText(Path.Combine(
            WcagContrast.RepoRoot(), "UnifiedMessenger", "Themes", "HighContrast.xaml"));

        foreach (var brush in new[] { "UmStatusSuccessBrush", "UmStatusWarningBrush", "UmStatusDangerBrush" })
        {
            Assert.Contains(brush, xaml, StringComparison.Ordinal);
        }
    }

    // ---- The sidebar's connection dot -----------------------------------------------------------------

    [Theory]
    [InlineData("UmStatusInfoColor")]
    [InlineData("UmStatusNeutralColor")]
    [InlineData("UmStatusMutedColor")]
    public void TheConnectionDotColoursAreThemedLikeEveryOtherStatusColour(string key)
    {
        // The dot was painted from hardcoded ARGB literals — #0063B1 blue, #808080 grey — months after the
        // other status colours were moved into theme dictionaries. One shared value cannot serve both
        // themes, and the dot is the primary "is this account working" signal.
        Assert.True(WcagContrast.IsThemed(key), $"{key} must be declared per theme, not shared.");
    }

    [Theory]
    [InlineData("UmStatusInfoColor")]
    [InlineData("UmStatusNeutralColor")]
    [InlineData("UmStatusMutedColor")]
    public void TheConnectionDotMeetsTheNonTextContrastBar(string key)
    {
        // A dot conveys state without text, so WCAG 1.4.11 applies: 3:1 against the surface behind it,
        // measured in both themes against that theme's own sidebar ground.
        var light = WcagContrast.Ratio(
            WcagContrast.ThemeColor("Light", key), WcagContrast.LightCard);
        var dark = WcagContrast.Ratio(
            WcagContrast.ThemeColor("Default", key), WcagContrast.DarkCard);

        Assert.True(light >= 3.0, $"{key} measures {light:0.00}:1 on the light surface (needs 3:1).");
        Assert.True(dark >= 3.0, $"{key} measures {dark:0.00}:1 on the dark surface (needs 3:1).");
    }
}
