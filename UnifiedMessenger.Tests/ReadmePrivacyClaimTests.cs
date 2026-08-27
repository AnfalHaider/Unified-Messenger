namespace UnifiedMessenger.Tests;

/// <summary>
/// Keeps the README's AI privacy claim honest about what the app actually sends.
/// </summary>
/// <remarks>
/// The README said prompts contain "aggregate counts only — never customer names or message text". That
/// is true of the dashboard insight strips and false of message triage, which puts the customer name and
/// up to 800 characters of the body into the prompt (<c>TranscriptBuilder.Build</c>). Nothing leaves the
/// machine either way — Ollama is loopback — but a buyer reading the README was told something about
/// their customers' data that the product does not do. AGENTS.md records the same trap and warns against
/// simplifying it back; this test is the part that fails instead of just warning.
/// </remarks>
public class ReadmePrivacyClaimTests
{
    private static string RepoFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, name);
        Assert.True(File.Exists(path), $"Missing {name} at {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Readme_DoesNotClaimPromptsAreAggregatesOnly()
    {
        var readme = RepoFile("README.md");

        Assert.DoesNotContain("aggregate counts only** — never customer names", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("never customer names or message text", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Readme_SaysThatTriageSendsTheCustomerNameAndMessage()
    {
        var readme = RepoFile("README.md");

        Assert.Contains("customer name", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("800 characters", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Readme_StillPromisesTheDataStaysOnTheMachine()
    {
        // The point is accuracy, not retreat: the local-only guarantee is the product, and it does hold.
        var readme = RepoFile("README.md");

        Assert.Contains("leaves the machine", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Readme_PointsAtTheInstallerCompilerThatActuallyExists()
    {
        var readme = RepoFile("README.md");

        Assert.DoesNotContain(@"LOCALAPPDATA\Programs\Inno Setup 6", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"Program Files (x86)\Inno Setup 6\ISCC.exe", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DoesNotPointAtALicenceFileThatIsNotThere()
    {
        var readme = RepoFile("README.md");

        Assert.DoesNotContain("See the repository license file", readme, StringComparison.OrdinalIgnoreCase);
    }
}
