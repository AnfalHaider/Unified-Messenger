using System.Text.RegularExpressions;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Guards the shape of the design system against drift.
///
/// <para>
/// A one-off cleanup does not hold. Corner radii had reached <b>six</b> distinct values across the app —
/// cards rendered at 8, 10, 12 <i>and</i> 14 — which is not a system, it is an accumulation. These tests
/// are what stop the next one-off value being added without a decision.
/// </para>
/// </summary>
public class DesignScaleTests
{
    /// <summary>Small controls and chips · medium surfaces · cards. Three tiers, deliberately.</summary>
    /// <remarks>
    /// <c>0</c> is square (an explicit choice, not a stray value), <c>2</c> is <c>UmCornerRadiusXsValue</c>
    /// — a token that already existed in <c>Tokens.xaml</c> and that this list simply failed to name — and
    /// <c>999</c> is the pill/capsule idiom, where the radius is deliberately larger than the control so the
    /// ends round fully.
    /// </remarks>
    private static readonly int[] CornerRadiusTiers = [2, 6, 8, 12];

    /// <summary>Not a rounding tier: an explicit square corner.</summary>
    private const int SquareRadius = 0;

    /// <summary>Not a rounding tier: deliberately larger than the control, so the ends round fully.</summary>
    private const int PillRadius = 999;

    private static readonly int[] AllowedCornerRadii = [.. CornerRadiusTiers, SquareRadius, PillRadius];

    private static IEnumerable<string> SourceFiles(string pattern)
    {
        var root = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");
        return Directory
            .EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SourceXaml() => SourceFiles("*.xaml");

    /// <summary>
    /// C# sources, excluding the scale definition itself — which is the one file allowed to hold numbers.
    /// </summary>
    private static IEnumerable<string> SourceCSharp() =>
        SourceFiles("*.cs").Where(f => !Path.GetFileName(f).Equals("UmScale.cs", StringComparison.Ordinal));

    [Fact]
    public void EveryCornerRadiusComesFromTheScale()
    {
        // XAML *and* C#. This read .xaml only, which is exactly the gap NoLiteralFontSizeInCode was written
        // to close for font sizes — its own comment calls the XAML-only scan "the reason the C# builders
        // accumulated ELEVEN distinct text sizes against the whole XAML surface's seven". The same thing had
        // happened here and nobody could see it: every XAML radius conformed, while the code side had drifted
        // to EIGHT distinct values (0, 2, 4, 5, 6, 8, 10, 12, 14, 15, 999) — worse than the six that
        // triggered the original cleanup. Snapped to the scale at v4.99.70.
        var offenders = new List<string>();

        foreach (var (file, text, pattern) in
            SourceXaml().Select(f => (f, File.ReadAllText(f), @"CornerRadius=""(\d+)"""))
                .Concat(SourceCSharp().Select(f => (f, File.ReadAllText(f), @"new CornerRadius\(\s*(\d+)\s*\)"))))
        {
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var value = int.Parse(match.Groups[1].Value);
                if (!AllowedCornerRadii.Contains(value))
                {
                    var line = text[..match.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line} uses radius {value}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Corner radii must come from the 0 / 2 / 6 / 8 / 12 scale (or the 999 pill idiom). Off-scale:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheScaleStaysSmallEnoughToBeAScale()
    {
        // Three tiers is a system a person can hold in their head. Adding a fourth should require deciding
        // what it is FOR, which is what failing this test forces — and it did, at v4.99.70.
        //
        // The fourth tier is 2, and the decision was already taken: Tokens.xaml has declared
        // UmCornerRadiusXsValue = 2 the whole time, for chips and small inline markers. This list said three
        // because it scanned XAML literals only and never saw either the token or the five C# uses of it.
        // So this is not a fourth value being added — it is the fourth that already existed being counted.
        //
        // 0 (square) and 999 (pill) are excluded from the count on purpose: neither is a rounding tier, and
        // folding them in would let the number creep while looking like a system.
        Assert.True(
            CornerRadiusTiers.Length <= 4,
            $"The radius scale has grown to {CornerRadiusTiers.Length} tiers. Decide what the new one is "
            + "FOR, and name it in Tokens.xaml, before raising this.");
    }

    [Fact]
    public void NoColourIsHardcodedInXaml()
    {
        // Already true, and worth pinning: a literal hex bypasses the theme dictionaries and will be wrong
        // in one of the two themes. This is the check that keeps it true.
        var offenders = new List<string>();

        foreach (var file in SourceXaml())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(
                         text, @"(Background|Foreground|Fill|BorderBrush|Stroke)=""#[0-9A-Fa-f]{6,8}"""))
            {
                var line = text[..match.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line} — {match.Value}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Colours must come from ThemeResource, not literals:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryIconOnlyControlIsNamedForAScreenReader()
    {
        // A control whose content is a glyph derives no accessible name — a screen reader announces only
        // "button". Already clean across every source XAML file; this keeps it that way.
        var offenders = new List<string>();

        foreach (var file in SourceXaml())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(
                         text, @"<(Button|ToggleButton|HyperlinkButton|AppBarButton)\b([^>]*?)(/>|>)",
                         RegexOptions.Singleline))
            {
                var attributes = match.Groups[2].Value;
                if (attributes.Contains("AutomationProperties.Name", StringComparison.Ordinal))
                {
                    continue;
                }

                var body = text.Substring(match.Index, Math.Min(500, text.Length - match.Index));
                var iconOnly = (body.Contains("FontIcon", StringComparison.Ordinal)
                                || body.Contains("SymbolIcon", StringComparison.Ordinal))
                               && !attributes.Contains("Content=\"", StringComparison.Ordinal);

                if (iconOnly)
                {
                    var line = text[..match.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Icon-only controls need AutomationProperties.Name:\n  " + string.Join("\n  ", offenders));
    }

    // ---- The type / icon scale, guarded in BOTH languages ---------------------------------------------

    [Fact]
    public void NoLiteralFontSizeInXaml()
    {
        var offenders = new List<string>();

        foreach (var file in SourceXaml())
        {
            // Tokens.xaml and Typography.xaml are where the numbers legitimately live.
            if (file.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"FontSize=""(\d+)"""))
            {
                var line = text[..match.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line} — {match.Value}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Font sizes must come from the ramp (UmFontSize*) or the icon scale (UmIconSize*):\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NoLiteralFontSizeInCode()
    {
        // The guard that did not exist, and the reason the C# builders accumulated ELEVEN distinct text
        // sizes against the whole XAML surface's seven. `DesignScaleTests` read .xaml only, so every
        // literal in CommandCenterPanel.xaml.cs and friends was invisible to it.
        var offenders = new List<string>();

        foreach (var file in SourceCSharp())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"FontSize\s*=\s*(\d+(?:\.\d+)?)\b"))
            {
                var line = text[..match.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line} — {match.Value.Trim()}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Code-built controls must use UmScale.Text.* or UmScale.Icon.*, not literals:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NoFontIconShipsWithAnEmptyGlyph()
    {
        // EIGHT of the app's 44 code-built icon glyphs reached the repo as the empty string, from the
        // initial commit onward. A FontIcon with a zero-length Glyph draws NOTHING while the control around
        // it stays present, laid out, focusable and clickable — so the failure is invisible in every way a
        // test or a code reading can see, and invisible on screen too.
        //
        // What it cost: the per-account details button on the dashboard (tooltip "Account details — reply
        // speed, backlog, and who's waiting") was a 12-pixel sliver of pure button padding. The L1
        // drill-down behind it shipped in v4.53.0 and could not be found by looking; it was reached here
        // only by computing where the button had to be from the layout and clicking that. The same defect
        // blanked the mark-done split button, which in COMPACT density has no text label either — so the
        // one control that closes an awaiting conversation had no visible presence at all.
        //
        // Write glyphs as "", never as an inline character: all eight blanks were in the inline form,
        // and a private-use codepoint pasted into source is one encoding round-trip away from vanishing.
        var offenders = new List<string>();

        foreach (var file in SourceCSharp())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"(?:Glyph|IconGlyph)\s*=\s*""([^""]*)"""))
            {
                if (match.Groups[1].Value.Length != 0)
                {
                    continue;
                }

                var line = text[..match.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These FontIcons have an empty Glyph and will render as nothing, leaving an invisible but "
            + "still-clickable control:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheCodeScaleMatchesTheXamlTokens()
    {
        // UmScale duplicates Tokens.xaml so that code-built controls never make a UI-thread WinRT call to
        // read a resource (the mistake that terminated the process once already — see UmSemanticBrushes).
        // Duplication is only safe while something proves the copies agree.
        //
        // Constant first, XAML second: xUnit's analyser treats the constant as the expected value, and the
        // other order raised twelve xUnit2000 warnings that showed up as annotations on every CI run —
        // noise that makes a genuinely failing build harder to read.
        var tokensPath = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger", "Themes", "Tokens.xaml");
        var tokens = File.ReadAllText(tokensPath);

        double FromXaml(string key)
        {
            var match = Regex.Match(tokens, $@"x:Key=""{key}"">([\d.]+)<");
            Assert.True(match.Success, $"Token '{key}' is missing from Tokens.xaml.");
            return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.Equal(UmScale.Text.Caption, FromXaml("UmFontSizeCaption"));
        Assert.Equal(UmScale.Text.Body, FromXaml("UmFontSizeBody"));
        Assert.Equal(UmScale.Text.BodyStrong, FromXaml("UmFontSizeBodyStrong"));
        Assert.Equal(UmScale.Text.Subtitle, FromXaml("UmFontSizeSubtitle"));
        Assert.Equal(UmScale.Text.Title, FromXaml("UmFontSizeTitle"));
        Assert.Equal(UmScale.Text.Metric, FromXaml("UmFontSizeMetric"));
        Assert.Equal(UmScale.Text.Hero, FromXaml("UmFontSizeHero"));

        Assert.Equal(UmScale.Icon.Sm, FromXaml("UmIconSizeSm"));
        Assert.Equal(UmScale.Icon.Md, FromXaml("UmIconSizeMd"));
        Assert.Equal(UmScale.Icon.Lg, FromXaml("UmIconSizeLg"));
        Assert.Equal(UmScale.Icon.Xl, FromXaml("UmIconSizeXl"));

        Assert.Equal(UmScale.Space.Xs, FromXaml("UmSpacingXs"));
        Assert.Equal(UmScale.Space.Sm, FromXaml("UmSpacingSm"));
        Assert.Equal(UmScale.Space.Md, FromXaml("UmSpacingMd"));
        Assert.Equal(UmScale.Space.Lg, FromXaml("UmSpacingLg"));
        Assert.Equal(UmScale.Space.Xl, FromXaml("UmSpacingXl"));
        Assert.Equal(UmScale.Space.Xxl, FromXaml("UmSpacingXxl"));
    }

    [Fact]
    public void TheTypeRampStaysSevenStepsAndTheIconScaleFour()
    {
        // Adding an eighth type step or a fifth icon step should require deciding what it is FOR, which is
        // what failing this forces. Twelve text sizes is what the app had before this scale existed.
        double[] ramp =
        [
            UmScale.Text.Caption, UmScale.Text.Body, UmScale.Text.BodyStrong, UmScale.Text.Subtitle,
            UmScale.Text.Title, UmScale.Text.Metric, UmScale.Text.Hero
        ];
        double[] icons = [UmScale.Icon.Sm, UmScale.Icon.Md, UmScale.Icon.Lg, UmScale.Icon.Xl];

        Assert.Equal(7, ramp.Distinct().Count());
        Assert.Equal(4, icons.Distinct().Count());

        // Strictly ascending — a ramp with a flat or backward step is not a ramp.
        Assert.Equal(ramp.OrderBy(v => v), ramp);
        Assert.Equal(icons.OrderBy(v => v), icons);

        // Nothing below 11: the app previously shipped 9px and 10px text.
        Assert.True(ramp.Min() >= 11, "Body text must never be smaller than 11px.");
    }

    [Fact]
    public void EverySpacingValueSitsOnTheFourPixelGrid()
    {
        var offenders = new List<string>();

        foreach (var file in SourceXaml())
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"(Padding|Margin)=""([^""{}]+)"""))
            {
                var parts = match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries);
                if (!parts.All(p => double.TryParse(p, out _)))
                {
                    continue;
                }

                var offGrid = parts
                    .Select(double.Parse)
                    .Where(v => v % 4 != 0)
                    .ToList();

                if (offGrid.Count > 0)
                {
                    var line = text[..match.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line} — {match.Value}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Spacing must sit on the 4px grid (see docs/design-system/scales.md):\n  "
            + string.Join("\n  ", offenders));
    }
}
