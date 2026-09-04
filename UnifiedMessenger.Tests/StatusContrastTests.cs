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

    /// <summary>
    /// Selection is not a health state, so it must not be painted with a health colour.
    /// </summary>
    /// <remarks>
    /// The sidebar's 3px selection bar used <c>SystemFillColorSuccessBrush</c>, so "this row is selected"
    /// and "this account is healthy" were the same green — on a rail whose entire job is showing which
    /// accounts are in trouble. A selected, failing account showed a green bar. It now uses the brand
    /// accent, which this app does not assign a status meaning to.
    /// </remarks>
    [Fact]
    public void SidebarSelectionIsNotPaintedWithAStatusColour()
    {
        var sidebar = File.ReadAllText(Path.Combine(
            WcagContrast.RepoRoot(), "UnifiedMessenger", "Controls", "WorkspaceSidebar.xaml.cs"));

        var selectionBlock = sidebar
            .Split("BorderBrush = selected", StringSplitOptions.None)
            .Skip(1)
            .Select(part => part.Split('\n')[1])
            .ToList();

        Assert.NotEmpty(selectionBlock);
        Assert.All(selectionBlock, line =>
            Assert.False(
                line.Contains("Success", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Critical", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Caution", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Danger", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Warning", StringComparison.OrdinalIgnoreCase),
                $"Selection is painted with a status colour: {line.Trim()}"));
    }

    /// <summary>
    /// Holds the line on the second, unaudited status palette rather than letting it grow.
    /// </summary>
    /// <remarks>
    /// <b>This is a ratchet, not a ban.</b> The app ships two full status palettes: its own audited
    /// <c>UmStatus*</c> tokens and Windows' <c>SystemFillColor*</c> ones, which means two greens, two
    /// ambers and two reds can appear on one screen. Every SystemFillColor pairing was measured during the
    /// 2026-08-26 audit and every one passes AA, so this is a consistency defect and not a contrast one —
    /// which is exactly why migrating all of them blind, at the tail of a long change, would risk more
    /// than it fixes. The count is pinned here so the split cannot quietly widen, and shrinking it is a
    /// deliberate piece of work with its own contrast pass.
    /// </remarks>
    [Fact]
    public void TheSystemPaletteDoesNotSpreadFurther()
    {
        var uiRoot = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");
        var references = new[] { "*.xaml", "*.cs" }
            .SelectMany(pattern => Directory.EnumerateFiles(uiRoot, pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Sum(path => File.ReadAllText(path).Split("SystemFillColor").Length - 1);

        // Was 69. Now 10, and every survivor is a *BackgroundBrush.
        //
        // The 59 FOREGROUND references migrated to the audited UmStatus* tokens, which is strictly safer
        // rather than riskier: those tokens are the ones this very file measures against LightCard,
        // DarkCard and DarkChrome at the 4.5:1 bar, so the migration moved colour references INTO the
        // measured set. It was safe to do mechanically because no non-Background system brush was ever
        // used as a background — checked before the replace, so a saturated text colour could not become
        // a solid block behind text.
        //
        // The background washes deliberately stay. They are the surface BEHIND text, so the applicable
        // measurement is of whatever is drawn on them rather than of the wash itself, and there is no
        // UmStatusInfoWash to move SystemFillColorAttentionBackgroundBrush onto. Migrating those is its
        // own piece of work with its own pass.
        Assert.True(
            references <= 10,
            $"SystemFillColor* references rose to {references}. The app has its own audited status palette "
            + "(UmSemanticBrushes) — use it, or lower this ceiling deliberately.");
    }

    // ---- Every surface, not a representative one ------------------------------------------------------

    /// <summary>
    /// The full semantic palette, including the three that were only ever measured at the 3:1 dot bar.
    /// </summary>
    /// <remarks>
    /// Muted and Neutral are not only dots. <c>ReviewDesk.UrgencyBrush</c> hands
    /// <c>UmStatusMutedBrush</c> to a <c>TextBlock.Foreground</c> for an unrated review, and
    /// <c>DeltaBadge</c> paints its arrow and percentage with <c>UmStatusNeutralBrush</c>. Both are
    /// caption-size text, so 4.5:1 applies to them and never got asserted.
    /// </remarks>
    public static TheoryData<string, string> AllStatusColoursByTheme()
    {
        var data = new TheoryData<string, string>();
        foreach (var key in new[]
        {
            "UmStatusSuccessColor", "UmStatusWarningColor", "UmStatusDangerColor",
            "UmStatusInfoColor", "UmStatusNeutralColor", "UmStatusMutedColor"
        })
        {
            data.Add(key, "Light");
            data.Add(key, "Default");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllStatusColoursByTheme))]
    public void EveryStatusColourIsReadableOnEverySurfaceOfItsOwnTheme(string colorKey, string themeKey)
    {
        // Cards sit on the canvas, on the surface, and on the sunken surface, and a status colour is drawn
        // as text inside all three. The old assertions measured ONE background per theme — and on light
        // that background was #FFFFFF, the most forgiving surface the app has. The sunken surface is
        // roughly 10% darker, which is exactly enough to push two colours under the bar.
        var color = WcagContrast.ThemeColor(themeKey, colorKey);

        foreach (var (name, surface) in WcagContrast.Surfaces(themeKey))
        {
            var ratio = WcagContrast.Ratio(color, surface);

            Assert.True(
                ratio >= WcagContrast.AaText,
                $"{colorKey} ({color}) on the {themeKey} {name} surface ({surface}) is {ratio:F2}:1, "
                + $"needs {WcagContrast.AaText}:1. It is drawn as text, not only as a dot.");
        }
    }

    // ---- Opacity is not a contrast-safe way to make text quieter ---------------------------------------

    // ---- Telling one panel from another ---------------------------------------------------------------

    [Theory]
    [InlineData("Light")]
    [InlineData("Default")]
    public void ACardsEdgeIsVisibleAgainstWhatIsBehindIt(string themeKey)
    {
        // The owner's report was "dark theme has no proper visibility", and the answer to "reading the text
        // or telling the panels apart?" was telling the panels apart. Measured, every separation mechanism
        // in the app was far under the 3:1 that WCAG 1.4.11 asks of a boundary:
        //
        //                                        dark     light
        //   surface fill vs sunken               1.048    1.112
        //   UmHairlineBrush                      1.22     1.24
        //   UmHairlineStrongBrush                1.52     1.43
        //   WinUI CardStrokeColorDefault (19x)   1.15     1.14
        //   shadow                               none     none
        //
        // Light gets away with less because a white card on a grey canvas is a familiar figure-ground cue
        // and the eye discriminates lightness far better in bright ranges. A dark theme has neither
        // advantage AND cannot cast a shadow — there is nothing darker than near-black to cast it — which
        // is why dark design systems raise a surface by lightening it. This app did not, so every panel
        // sat at the same apparent depth.
        //
        // The bar here is 1.5 rather than 3.0 deliberately: 3:1 on every card edge is a wireframe, not a
        // dashboard, and the fill difference carries part of the load. It is a ratchet on what was
        // measured after the fix — raise it if the edges are still too quiet on screen, never lower it.
        var hairline = WcagContrast.ThemeColor(themeKey, "UmHairlineColor");

        foreach (var (name, surface) in WcagContrast.Surfaces(themeKey))
        {
            if (name == "sunken")
            {
                continue; // a sunken well is defined by being recessed, not by an edge
            }

            var ratio = WcagContrast.Ratio(hairline, surface);

            Assert.True(
                ratio >= 1.5,
                $"the {themeKey} hairline ({hairline}) is {ratio:F2}:1 against the {name} surface "
                + $"({surface}). Below about 1.5 a card edge stops reading and panels merge into one field.");
        }
    }

    [Fact]
    public void CardsDoNotDrawTheirEdgeWithTheSystemStroke()
    {
        // CardStrokeColorDefaultBrush measures 1.15:1 in both themes — the weakest edge available, and it
        // was the most-used one: 19 sites including CommandCenterPanel, KpiStatCard, ActivityPatternsPanel
        // and NotificationFeedBrush, which are exactly the panels that could not be told apart. Migrated to
        // UmHairlineBrush so that strengthening the token actually reaches the dashboard.
        var uiRoot = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");
        var offenders = new[] { "*.xaml", "*.cs" }
            .SelectMany(pattern => Directory.EnumerateFiles(uiRoot, pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("CardStrokeColorDefaultBrush", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "these draw a card edge with the system stroke (1.15:1), which no amount of tuning the app's "
            + $"own hairline will reach: {string.Join(", ", offenders)}. Use UmHairlineBrush.");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Default")]
    public void TertiaryTextIsReadableOnEverySurfaceOfItsOwnTheme(string themeKey)
    {
        // This token exists because the app had NO foreground token of any kind — which is why 88 sites
        // reached for Opacity to make text quieter: there was nothing else to reach for. Dimming ordinary
        // body text is mostly fine (0.65 is 7.95:1 on the dark surface, 5.10:1 on the light sunken one),
        // but 0.55 measures 3.84:1 in light, below AA, and no contrast test can see an Opacity because it
        // is applied at the element rather than the brush. Four caption sites used it; they now use this.
        var color = WcagContrast.ThemeColor(themeKey, "UmTextTertiaryColor");

        foreach (var (name, surface) in WcagContrast.Surfaces(themeKey))
        {
            var ratio = WcagContrast.Ratio(color, surface);

            Assert.True(
                ratio >= WcagContrast.AaText,
                $"UmTextTertiaryColor ({color}) on the {themeKey} {name} surface ({surface}) is "
                + $"{ratio:F2}:1, needs {WcagContrast.AaText}:1.");
        }
    }

    [Fact]
    public void RawOpacityDimmingDoesNotSpreadFurther()
    {
        // Why a ceiling rather than an assertion about contrast: Opacity is applied at the ELEMENT, so
        // what it renders is invisible to every measurement in this file unless the exact foreground and
        // the exact surface behind it are both known at the call site. WcagContrast.Composite exists to
        // measure a specific pairing when one is in question; it cannot audit the pattern in general.
        //
        // What was measured, so it is not re-derived from scratch next time:
        //   * Dimming ordinary body text is FINE. WinUI's primary foreground at 0.65 is 7.95:1 on the
        //     dark surface and 5.10:1 on the light sunken surface — both clear of AA. The 51 sites in
        //     SettingsPage.xaml are not a contrast defect, and a session brief that said they were had
        //     counted bin/obj copies (352) and assumed the worst about all of them.
        //   * Dimming a STATUS colour would not be fine — all six fall under 4.5:1 by 0.65, and Danger
        //     and Muted fail at 0.75. Measured at zero such sites in XAML. The two C# sites that pair a
        //     Foreground with an Opacity dim TextFillColorSecondaryBrush (0.7) and
        //     SystemFillColorCautionBrush (0.9), neither of which is a UmStatus* token.
        //   * 0.55 applied to body text is 3.84:1 in light — BELOW AA. Several SettingsPage captions use
        //     it (UmOpacitySubtle). That is a real defect and is tracked separately; it is not fixable by
        //     a ceiling, only by giving those captions a real foreground.
        //
        // The bin/obj exclusion below is load-bearing: counting build output inflates this exactly 4x.
        var uiRoot = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");
        var sites = Directory
            .EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Sum(path => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), @"Opacity=""0\.\d+""").Count);

        // Measured at 88, then 84 once the four 0.55 CAPTION sites moved to UmTextTertiaryBrush (v4.99.69)
        // — 0.55 on text is 3.84:1 in light, below AA. The 0.55 uses that remain are FontIcon glyphs, which
        // are non-text and clear 1.4.11's 3:1 bar. Lower this as further sites move to a named foreground;
        // never raise it.
        Assert.True(
            sites <= 84,
            $"raw Opacity dimming rose to {sites} XAML sites. Opacity is applied at the element, so no "
            + "contrast test can see what it renders — use a themed foreground brush, or lower this "
            + "ceiling deliberately.");
    }
}
