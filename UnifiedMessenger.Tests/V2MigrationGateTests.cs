namespace UnifiedMessenger.Tests;

public class V2MigrationGateTests
{
    private static readonly string[] ShellSingletonForbiddenPatterns =
    [
        "AppSettingsService.Instance",
        "MessageAnalyticsService.Instance",
        "NotificationHub.Instance",
        "ThreadRegistryService.Instance"
    ];

    [Fact]
    public void ShellLayer_DoesNotUseForbiddenSingletons()
    {
        var repoRoot = FindRepoRoot();
        var shellDir = Path.Combine(repoRoot, "UnifiedMessenger", "Services", "Shell");
        Assert.True(Directory.Exists(shellDir), $"Missing shell directory: {shellDir}");

        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(shellDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var pattern in ShellSingletonForbiddenPatterns)
            {
                if (text.Contains(pattern, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetFileName(file)}: {pattern}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void MainWindow_IsUnderLineBudget()
    {
        var repoRoot = FindRepoRoot();
        var mainWindow = Path.Combine(repoRoot, "UnifiedMessenger", "MainWindow.xaml.cs");
        var lineCount = File.ReadAllLines(mainWindow).Length;
        Assert.True(lineCount <= 450, $"MainWindow.xaml.cs has {lineCount} lines; budget is 450.");
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
