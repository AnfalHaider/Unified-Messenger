using System.Text.RegularExpressions;

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

    private static IEnumerable<string> SourceXaml()
    {
        var root = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");
        return Directory
            .EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase));
    }

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
}
