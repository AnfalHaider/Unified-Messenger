using System.Text.RegularExpressions;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Guards for the consolidated design system (Increment 121).
///
/// <para>
/// <b>What was already safe.</b> The numeric scale is cross-checked — <c>DesignScaleTests</c> parses
/// <c>Tokens.xaml</c> and asserts every value in <c>UmScale</c> matches it, so the C# mirror cannot drift
/// from the XAML definition. Nothing here duplicates that.
/// </para>
/// <para>
/// <b>What was not.</b> Brush keys are raw strings at every call site — <c>Brush("UmSurfaceSunkenBrush")</c>
/// — and no test ever checked that the named brush exists. A typo does not fail the build and does not
/// throw; the lookup misses and the element silently falls back, which is the same class of failure as the
/// eight private brush lookups that kept reintroducing white-in-dark surfaces through v4.99.61–63. It is
/// invisible in exactly the theme the author was not looking at.
/// </para>
/// </summary>
public class DesignTokenConsolidationTests
{
    private static string TokensPath =>
        Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger", "Themes", "Tokens.xaml");

    private static string AppRoot => Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");

    private static IEnumerable<string> AppSourceFiles(string pattern) =>
        Directory.EnumerateFiles(AppRoot, pattern, SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>
    /// Reads a brush declared as <c>&lt;SolidColorBrush x:Key="X" Color="#RRGGBB" /&gt;</c> from one theme
    /// dictionary. <see cref="WcagContrast.ThemeColor"/> only reads the <c>&lt;Color&gt;</c> element form,
    /// and the washes are declared as brushes alone.
    /// </summary>
    private static string ThemeBrushColor(string themeKey, string brushKey)
    {
        var xaml = File.ReadAllText(TokensPath);
        var themeStart = xaml.IndexOf($"x:Key=\"{themeKey}\"", StringComparison.Ordinal);
        Assert.True(themeStart >= 0, $"No '{themeKey}' theme dictionary in Tokens.xaml.");

        var block = xaml[themeStart..];
        var end = block.IndexOf("</ResourceDictionary>", StringComparison.Ordinal);
        block = end > 0 ? block[..end] : block;

        var match = Regex.Match(
            block,
            $@"SolidColorBrush x:Key=""{Regex.Escape(brushKey)}"" Color=""(#[0-9A-Fa-f]{{6}})""");
        Assert.True(match.Success, $"'{brushKey}' is not defined in the '{themeKey}' theme dictionary.");
        return match.Groups[1].Value;
    }

    [Fact]
    public void EveryBrushKeyNamedInCodeExists()
    {
        var tokens = File.ReadAllText(TokensPath);
        var defined = Regex.Matches(tokens, @"SolidColorBrush x:Key=""([A-Za-z]+)""")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var used = AppSourceFiles("*.cs")
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"""(Um[A-Za-z]*Brush)""")
                .Select(m => (Key: m.Groups[1].Value, File: Path.GetFileName(path))))
            .ToList();

        Assert.NotEmpty(used);

        var missing = used
            .Where(u => !defined.Contains(u.Key))
            .Select(u => $"{u.Key} (in {u.File})")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // A missing key does not throw. The lookup misses, the element keeps whatever brush it had, and
        // the result is a surface with the wrong theme's colour — visible only to someone running the
        // theme the author was not in.
        Assert.True(
            missing.Count == 0,
            "These brush keys are named in code but defined nowhere in Tokens.xaml, so the lookup will "
            + "silently fall back:\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void EveryBrushIsDefinedInBothThemesOrNeither()
    {
        var xaml = File.ReadAllText(TokensPath);
        var themeBlockEnd = xaml.IndexOf("</ResourceDictionary.ThemeDictionaries>", StringComparison.Ordinal);
        Assert.True(themeBlockEnd > 0, "Tokens.xaml must declare theme dictionaries.");

        var themed = xaml[..themeBlockEnd];
        var lightStart = themed.IndexOf("x:Key=\"Light\"", StringComparison.Ordinal);
        var darkStart = themed.IndexOf("x:Key=\"Default\"", StringComparison.Ordinal);
        Assert.True(lightStart >= 0 && darkStart >= 0);

        var first = Math.Min(lightStart, darkStart);
        var second = Math.Max(lightStart, darkStart);
        var blockA = themed[first..second];
        var blockB = themed[second..];

        HashSet<string> Keys(string block) =>
            Regex.Matches(block, @"SolidColorBrush x:Key=""([A-Za-z]+)""")
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

        var a = Keys(blockA);
        var b = Keys(blockB);
        var oneSided = a.Except(b).Concat(b.Except(a)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        // A brush defined in one theme only is the classic unreadable-surface bug: it resolves in the
        // theme it was authored in and falls back in the other, so the page renders one theme's text on
        // the other theme's ground.
        Assert.True(
            oneSided.Count == 0,
            "These brushes are themed on one side only, so they fall back in the other theme:\n  "
            + string.Join("\n  ", oneSided));
    }

    [Theory]
    [InlineData("UmStatusSuccessBrush", "UmStatusSuccessWashBrush")]
    [InlineData("UmStatusWarningBrush", "UmStatusWarningWashBrush")]
    [InlineData("UmStatusDangerBrush", "UmStatusDangerWashBrush")]
    [InlineData("UmStatusInfoBrush", "UmStatusInfoWashBrush")]
    [InlineData("UmStatusMutedBrush", "UmStatusNeutralWashBrush")]
    public void EachStatusColourIsReadableOnItsOwnWash(string textKey, string washKey)
    {
        foreach (var theme in new[] { "Light", "Default" })
        {
            var text = ThemeBrushColor(theme, textKey);
            var wash = ThemeBrushColor(theme, washKey);
            var ratio = WcagContrast.Ratio(text, wash);

            // The washes existed for three releases with no contrast test at all, on the reasoning that a
            // background is measured by whatever is drawn on it. True — and nothing was measuring that
            // either. A chip is precisely this pairing: status text on its own status wash.
            Assert.True(
                ratio >= WcagContrast.AaText,
                $"{textKey} on {washKey} in the {theme} theme is {ratio:F2}:1, below the {WcagContrast.AaText}:1 bar.");
        }
    }

    [Fact]
    public void TheWashSetIsCompleteSoNoSurfaceNeedsToLeaveThePalette()
    {
        // The Windows system fill brushes survived the v4.99.36 migration for one stated reason: there was
        // no audited token to move an attention or neutral background onto. Completing the set is what
        // turned that ceiling from ten into zero, so the set has to stay complete.
        foreach (var key in new[]
        {
            "UmStatusSuccessWashBrush",
            "UmStatusWarningWashBrush",
            "UmStatusDangerWashBrush",
            "UmStatusInfoWashBrush",
            "UmStatusNeutralWashBrush"
        })
        {
            ThemeBrushColor("Light", key);
            ThemeBrushColor("Default", key);
        }
    }

    [Fact]
    public void NoCodeBuiltBrushIsResolvedFromApplicationResources()
    {
        var offenders = new List<string>();

        foreach (var path in AppSourceFiles("*.cs"))
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains("Application.Current.Resources[", StringComparison.Ordinal))
                {
                    continue;
                }

                // Styles are fine — they are not theme-resolved per element, and AccentButtonStyle is the
                // framework's own. It is BRUSHES that break, because this lookup resolves against the
                // app-default theme rather than the element's.
                if (!line.Contains("Brush", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{Path.GetFileName(path)}:{i + 1}");
            }
        }

        // Application.Current.Resources resolves the APP-default theme, and this app themes the window
        // root rather than the application — so it reads Light even in dark mode. That covers every Um*
        // token too, because Tokens.xaml declares them inside ThemeDictionaries. Eight files had each
        // rolled their own private lookup, which is why white-in-dark surfaces kept reappearing after
        // each fix across v4.99.61–63. ThemeBrushResolver.Resolve(element, key) is the one way in.
        Assert.True(
            offenders.Count == 0,
            "These resolve a brush from application resources, which reads the app-default theme rather "
            + "than the element's. Use ThemeBrushResolver.Resolve:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void SemanticBrushConstantsPointAtRealTokens()
    {
        var tokens = File.ReadAllText(TokensPath);

        foreach (var field in typeof(UmSemanticBrushes)
                     .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                     .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
        {
            var key = (string?)field.GetRawConstantValue();
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("Um", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                tokens.Contains($@"x:Key=""{key}""", StringComparison.Ordinal),
                $"UmSemanticBrushes.{field.Name} names '{key}', which Tokens.xaml does not define.");
        }
    }
}
