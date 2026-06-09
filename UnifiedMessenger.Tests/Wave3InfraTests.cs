namespace UnifiedMessenger.Tests;

public class Wave3InfraTests
{
    [Fact]
    public void BuildWorkflow_DefinesPackageDrivenReleaseJob()
    {
        var workflowPath = Path.Combine(
            FindRepoRoot(),
            ".github",
            "workflows",
            "build.yml");

        var content = File.ReadAllText(workflowPath);

        Assert.Contains("needs: package", content, StringComparison.Ordinal);
        Assert.Contains("download-artifact@v4", content, StringComparison.Ordinal);
        Assert.Contains("installer-win-x64", content, StringComparison.Ordinal);
        Assert.Contains("ui-smoke:", content, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Release tag must include",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeHarness_NoLongerReferencesLegacyBranchFilter()
    {
        var harnessPath = Path.Combine(
            FindRepoRoot(),
            "UnifiedMessenger.UiSmokeTests",
            "ModuleValidationHarness.cs");

        var content = File.ReadAllText(harnessPath);

        Assert.DoesNotContain("Branch filter", content, StringComparison.Ordinal);
        Assert.Contains("Branch workspace pills", content, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "UnifiedMessenger.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }
}
