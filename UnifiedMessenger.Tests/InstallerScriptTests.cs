namespace UnifiedMessenger.Tests;

public class InstallerScriptTests
{
    [Theory]
    [InlineData("installer.iss")]
    [InlineData("installer-arm64.iss")]
    public void InstallerScript_UsesPerUserLocalAppDataInstall(string scriptName)
    {
        var script = ReadInstallerBundle(scriptName);

        Assert.Contains("{localappdata}", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"\Programs\UnifiedMessenger", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CleanAppPayload", script, StringComparison.Ordinal);
        Assert.DoesNotContain("{autopf}", script, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("installer.iss")]
    [InlineData("installer-arm64.iss")]
    public void InstallerScript_ConfiguresUpgradeSafety(string scriptName)
    {
        var script = ReadInstallerBundle(scriptName);

        Assert.Contains("CloseApplications=yes", script, StringComparison.Ordinal);
        Assert.Contains("CloseApplicationsFilter=", script, StringComparison.Ordinal);
        Assert.Contains("AppMutex=", script, StringComparison.Ordinal);
        Assert.Contains("UnifiedMessenger_AppMutex", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerScript_PostInstallLaunchIsOptIn()
    {
        var script = ReadInstallerBundle("installer.iss");

        Assert.Contains("postinstall", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unchecked", script, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("installer.iss")]
    [InlineData("installer-arm64.iss")]
    public void InstallerScript_DoesNotBundleOllamaRuntime(string scriptName)
    {
        var script = ReadInstallerBundle(scriptName);

        Assert.DoesNotContain("ai_runtime", script, StringComparison.Ordinal);
        Assert.DoesNotContain("extractarchive", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ollama-windows", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("third_party\\ollama", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExternalSize:", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerShared_PreservesOllamaUserDataOnUpgrade()
    {
        var script = ReadInstallerShared();

        Assert.Contains("CompareText(DirName, 'ollama')", script, StringComparison.Ordinal);
        // Version-agnostic: assert a version is defined (don't pin an outdated literal that breaks on every bump).
        Assert.Matches(@"#define\s+MyAppVersion\s+""\d+\.\d+\.\d+""", script);
    }

    [Fact]
    public void InstallerShared_RemovesRuntimeOnUninstallButKeepsModelsByDefault()
    {
        var script = ReadInstallerShared();

        Assert.Contains("Name: \"{#OllamaRuntimeDir}\"", script, StringComparison.Ordinal);
        Assert.Contains("Name: \"{#OllamaModelsDir}\"", script, StringComparison.Ordinal);
        Assert.Contains("uninstallremoveaimodels", script, StringComparison.Ordinal);
        Assert.Contains("TaskKill('ollama.exe')", script, StringComparison.Ordinal);
    }

    private static string ReadInstallerShared()
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), "installer-shared.iss"));
    }

    private static string ReadInstallerBundle(string scriptName)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, "installer-shared.iss"))
            + Environment.NewLine
            + File.ReadAllText(Path.Combine(root, scriptName));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UnifiedMessenger.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root (UnifiedMessenger.sln).");
    }
}
