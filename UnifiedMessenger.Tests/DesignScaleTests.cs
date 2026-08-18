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
    private static readonly int[] AllowedCornerRadii = [6, 8, 12];

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
        var offenders = new List<string>();

        foreach (var file in SourceXaml())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"CornerRadius=""(\d+)"""))
            {
                var value = int.Parse(match.Groups[1].Value);
                if (!AllowedCornerRadii.Contains(value))
                {
                    var line = text[..match.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line} uses CornerRadius=\"{value}\"");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Corner radii must come from the 6 / 8 / 12 scale. Off-scale values:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheScaleStaysSmallEnoughToBeAScale()
    {
        // Three tiers is a system a person can hold in their head. Adding a fourth should require deciding
        // what it is FOR, which is what failing this test forces.
        Assert.True(AllowedCornerRadii.Length <= 3);
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
    public void TheCodeScaleMatchesTheXamlTokens()
    {
        // UmScale duplicates Tokens.xaml so that code-built controls never make a UI-thread WinRT call to
        // read a resource (the mistake that terminated the process once already — see UmSemanticBrushes).
        // Duplication is only safe while something proves the copies agree.
        var tokensPath = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger", "Themes", "Tokens.xaml");
        var tokens = File.ReadAllText(tokensPath);

        double FromXaml(string key)
        {
            var match = Regex.Match(tokens, $@"x:Key=""{key}"">([\d.]+)<");
            Assert.True(match.Success, $"Token '{key}' is missing from Tokens.xaml.");
            return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.Equal(FromXaml("UmFontSizeCaption"), UmScale.Text.Caption);
        Assert.Equal(FromXaml("UmFontSizeBody"), UmScale.Text.Body);
        Assert.Equal(FromXaml("UmFontSizeBodyStrong"), UmScale.Text.BodyStrong);
        Assert.Equal(FromXaml("UmFontSizeSubtitle"), UmScale.Text.Subtitle);
        Assert.Equal(FromXaml("UmFontSizeTitle"), UmScale.Text.Title);
        Assert.Equal(FromXaml("UmFontSizeMetric"), UmScale.Text.Metric);
        Assert.Equal(FromXaml("UmFontSizeHero"), UmScale.Text.Hero);

        Assert.Equal(FromXaml("UmIconSizeSm"), UmScale.Icon.Sm);
        Assert.Equal(FromXaml("UmIconSizeMd"), UmScale.Icon.Md);
        Assert.Equal(FromXaml("UmIconSizeLg"), UmScale.Icon.Lg);
        Assert.Equal(FromXaml("UmIconSizeXl"), UmScale.Icon.Xl);

        Assert.Equal(FromXaml("UmSpacingXs"), UmScale.Space.Xs);
        Assert.Equal(FromXaml("UmSpacingSm"), UmScale.Space.Sm);
        Assert.Equal(FromXaml("UmSpacingMd"), UmScale.Space.Md);
        Assert.Equal(FromXaml("UmSpacingLg"), UmScale.Space.Lg);
        Assert.Equal(FromXaml("UmSpacingXl"), UmScale.Space.Xl);
        Assert.Equal(FromXaml("UmSpacingXxl"), UmScale.Space.Xxl);
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
