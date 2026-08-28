
namespace UnifiedMessenger.Tests;

/// <summary>
/// Mirrors the "Enforce shell DI gate" step in <c>.github/workflows/build.yml</c>.
///
/// <para>
/// The shell layer takes its collaborators through <c>ApplicationServices</c>; reaching for a singleton
/// there re-introduces the global state the DI composition root exists to remove, and makes
/// <c>ShellController</c> untestable without standing up half the app.
/// </para>
/// <para>
/// <b>Why this test exists at all, given CI already checks it.</b> The rule lived <i>only</i> in the
/// workflow, so a local run of the full suite could be entirely green while the push failed. That is
/// exactly what happened: <c>StartupWarmCount</c> was written with an
/// <c>AppSettingsService.Instance</c> fallback, 1863 tests passed locally, and the tag build failed on a
/// step nothing local ran. A gate that only exists in CI teaches you to trust a green suite that is not
/// the gate. Keeping the list in both places is duplication worth paying for — and if the two ever
/// disagree, this test is the one a developer sees first.
/// </para>
/// </summary>
public class ShellLayerDiTests
{
    /// <summary>
    /// Kept character-for-character in step with the <c>$forbidden</c> array in the workflow.
    /// </summary>
    private static readonly string[] ForbiddenSingletons =
    [
        "AppSettingsService.Instance",
        "MessageAnalyticsService.Instance",
        "NotificationHub.Instance",
        "ThreadRegistryService.Instance",
        "WebViewProfileManager.Instance",
        "SystemTrayService.Instance",
        "AppNotificationService.Instance",
        "TaskbarBadgeService.Instance",
        "GitHubUpdateService.Instance"
    ];

    private static string ShellRoot() =>
        Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger", "Services", "Shell");

    [Fact]
    public void TheShellLayerReachesForNoSingletons()
    {
        var offenders = new List<string>();

        foreach (var path in Directory.EnumerateFiles(ShellRoot(), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            foreach (var singleton in ForbiddenSingletons)
            {
                if (text.Contains(singleton, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(path)} uses {singleton}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The shell layer takes its collaborators through ApplicationServices. CI fails on this too, in "
            + "the 'Enforce shell DI gate' step: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// If someone adds a singleton to the workflow's list, this fails until it is added here as well — so
    /// the local suite cannot quietly become weaker than the gate it is standing in for.
    /// </summary>
    [Fact]
    public void ThisTestStillMatchesTheWorkflowItMirrors()
    {
        var workflow = File.ReadAllText(
            Path.Combine(WcagContrast.RepoRoot(), ".github", "workflows", "build.yml"));

        var gate = workflow.IndexOf("Enforce shell DI gate", StringComparison.Ordinal);
        Assert.True(gate > 0, "The 'Enforce shell DI gate' step is gone from build.yml. Was it renamed?");

        var end = workflow.IndexOf("- name:", gate + 1, StringComparison.Ordinal);
        var step = end > gate ? workflow[gate..end] : workflow[gate..];

        var missing = ForbiddenSingletons.Where(s => !step.Contains(s, StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "Listed here but not in the workflow: " + string.Join(", ", missing));

        // The reverse direction: anything the workflow bans must be banned here too.
        var inWorkflow = System.Text.RegularExpressions.Regex
            .Matches(step, @"'([A-Za-z]+\.Instance)'")
            .Select(m => m.Groups[1].Value)
            .ToList();

        var unmirrored = inWorkflow.Except(ForbiddenSingletons, StringComparer.Ordinal).ToList();
        Assert.True(
            unmirrored.Count == 0,
            "The workflow bans singletons this test does not, so a local run is weaker than CI: "
            + string.Join(", ", unmirrored));
    }
}
