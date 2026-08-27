using System.Text.RegularExpressions;

namespace UnifiedMessenger.Tests;

/// <summary>
/// "Instance" is this codebase's internal noun. The product calls the same thing an account.
/// </summary>
/// <remarks>
/// The sidebar says "Add account" and the dialog it opens said "Add account" too — but right-clicking one
/// offered "Rename instance...", opened a dialog titled "Rename instance", and Narrator announced "Rename
/// instance dialog". The owner has no idea what an instance is; it is the type name leaking through the
/// UI. This scans the shipped strings rather than trusting a one-time sweep, because the sweep is exactly
/// the kind of thing that holds until the next dialog is added.
/// </remarks>
public class AccountVocabularyTests
{
    /// <summary>
    /// The single legitimate use: an Ollama *instance* is Ollama's own vocabulary, not ours.
    /// </summary>
    private static readonly string[] AllowedPhrases = ["Ollama instance"];

    private static string UiRoot() => Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");

    private static IEnumerable<string> UiFiles(string extension) =>
        new[] { "Pages", "Controls", "Dialogs" }
            .Select(dir => Path.Combine(UiRoot(), dir))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, extension, SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(UiRoot(), "Services", "Shell"), extension, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static bool IsAllowed(string value) =>
        AllowedPhrases.Any(allowed => value.Contains(allowed, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void NoVisibleXamlStringCallsAnAccountAnInstance()
    {
        // The attributes a user actually reads. x:Name / AutomationId are identifiers and are exempt.
        var visible = new Regex(
            @"(?:Text|Header|Title|Content|PlaceholderText|Description|ToolTipService\.ToolTip)\s*=\s*""([^""]*)""",
            RegexOptions.Compiled);

        var offenders = new List<string>();

        foreach (var file in UiFiles("*.xaml"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in visible.Matches(lines[i]))
                {
                    var value = match.Groups[1].Value;

                    // Binding expressions carry a property name, not prose.
                    if (value.StartsWith("{", StringComparison.Ordinal) || IsAllowed(value))
                    {
                        continue;
                    }

                    if (Regex.IsMatch(value, @"\binstances?\b", RegexOptions.IgnoreCase))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1} → \"{value}\"");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "User-visible XAML calls an account an \"instance\":\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NoScreenReaderNameCallsAnAccountAnInstance()
    {
        // AutomationProperties.SetName is what Narrator speaks — the half of this defect nobody sees.
        var setName = new Regex(@"SetName\([^,]+,\s*""([^""]*)""", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in UiFiles("*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in setName.Matches(lines[i]))
                {
                    var value = match.Groups[1].Value;
                    if (IsAllowed(value))
                    {
                        continue;
                    }

                    if (Regex.IsMatch(value, @"\binstances?\b", RegexOptions.IgnoreCase))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1} → \"{value}\"");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A screen reader would say \"instance\" where the screen says account:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NoMenuItemOrDialogMessageCallsAnAccountAnInstance()
    {
        // Code-built menu items and error titles — the context menu is entirely built this way.
        var literals = new Regex(@"(?:Text\s*=\s*|ShowErrorAsync\(\s*)""([^""]*)""", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in UiFiles("*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in literals.Matches(lines[i]))
                {
                    var value = match.Groups[1].Value;
                    if (IsAllowed(value))
                    {
                        continue;
                    }

                    if (Regex.IsMatch(value, @"\binstances?\b", RegexOptions.IgnoreCase))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1} → \"{value}\"");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A menu item or message calls an account an \"instance\":\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheAllowedPhraseIsStillActuallyUsed()
    {
        // Guards the exemption itself: if the Ollama copy ever changes, this list should shrink rather
        // than sit there quietly permitting the word again.
        var settings = File.ReadAllText(Path.Combine(UiRoot(), "Pages", "SettingsPage.xaml"));

        Assert.Contains("Ollama instance", settings, StringComparison.Ordinal);
    }
}
