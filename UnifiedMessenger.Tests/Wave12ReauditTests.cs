using System.Reflection;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class Wave12ReauditTests
{
    [Fact]
    public void ProgramCompletionCriteria_DocumentsExist()
    {
        var repoRoot = FindRepoRoot();

        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "architecture", "system-map.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "architecture", "adr", "README.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "validation", "remaining-issues-log.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "validation", "completion-criteria.md")));
    }

    [Fact]
    public void ProgramCompletionCriteria_AutomatableGatesPass()
    {
        var criteria = ProgramCompletionCriteria.Evaluate(FindRepoRoot());

        var required = criteria.Where(criterion => !criterion.IsDeferred).ToList();
        Assert.All(required, criterion => Assert.True(criterion.Passed, criterion.Description));

        var occDebt = criteria.Single(criterion => criterion.Id == "C8");
        Assert.True(occDebt.Passed);
        Assert.False(occDebt.IsDeferred);
    }

    [Fact]
    public void ProgramCompletionCriteria_AutomatableGatesPassButReleaseBlockedByOccDebt()
    {
        var criteria = ProgramCompletionCriteria.Evaluate(FindRepoRoot());

        Assert.True(ProgramCompletionCriteria.IsAutomatableGatePassed(criteria));
        Assert.True(ProgramCompletionCriteria.IsReleaseReady(criteria));
    }

    [Fact]
    public void Reaudit_ArchitectureIssuesA1AndA4_MitigationsPresent()
    {
        var repoRoot = FindRepoRoot();
        var dashboardPage = File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Pages", "DashboardPage.xaml.cs"));
        var occ = File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Controls", "OperationsCommandCenter.xaml.cs"));

        Assert.Contains("DashboardRefreshCoordinator", dashboardPage, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageTriageService.Instance.Changed +=", occ, StringComparison.Ordinal);
        Assert.Contains(
            "RefreshOperationalFlags(raiseChanged: false)",
            File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Pages", "DashboardPage.xaml.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Reaudit_TestAssembly_HasMinimumTestMethods()
    {
        var testMethodCount = typeof(Wave12ReauditTests).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Count(method =>
                method.GetCustomAttributes<FactAttribute>(inherit: false).Any() ||
                method.GetCustomAttributes<TheoryAttribute>(inherit: false).Any());

        Assert.True(testMethodCount >= ProgramCompletionCriteria.MinimumTestMethodCount);
    }

    [Fact]
    public void Reaudit_LegacyDeadCodePathsRemoved()
    {
        var repoRoot = FindRepoRoot();
        var unifiedMessenger = Path.Combine(repoRoot, "UnifiedMessenger");

        Assert.False(Directory.Exists(Path.Combine(unifiedMessenger, "UnifiedMessengerControlCenter")));
        Assert.False(File.Exists(Path.Combine(unifiedMessenger, "Services", "DashboardBranchFilterEntry.cs")));

        var helper = File.ReadAllText(Path.Combine(unifiedMessenger, "Services", "DashboardPageHelper.cs"));
        Assert.DoesNotContain("ApplyBranchScoped", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Reaudit_MvvmSurfaces_HaveViewModels()
    {
        var repoRoot = FindRepoRoot();
        var viewModels = Path.Combine(repoRoot, "UnifiedMessenger", "ViewModels");

        Assert.True(File.Exists(Path.Combine(viewModels, "OperationsCommandCenterViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(viewModels, "PersonalOverviewViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(viewModels, "SettingsViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(viewModels, "MainWindowViewModel.cs")));
    }

    [Fact]
    public void Reaudit_OpenInstance_IsCanonicalNavigationApi()
    {
        var navigationType = typeof(INavigationService);
        var openInstance = navigationType.GetMethod(nameof(INavigationService.OpenInstance), [typeof(string)]);

        Assert.NotNull(openInstance);
    }

    [Fact]
    public void Reaudit_NotificationAlert_SupportsConversationDeepLinks()
    {
        var alert = NotificationAlert.Create(
            "inst-1",
            "Sales",
            "whatsapp",
            "Invoice",
            conversationKey: "120363@s.whatsapp.net",
            customerName: "Alex");

        Assert.True(alert.HasConversationTarget);
    }

    [Fact]
    public void ProgramCompletionCriteria_OccLineCountTracked()
    {
        var lines = ProgramCompletionCriteria.CountLines(Path.Combine(
            FindRepoRoot(),
            "UnifiedMessenger",
            "Controls",
            "OperationsCommandCenter.xaml.cs"));

        Assert.True(lines < ProgramCompletionCriteria.OccCodeBehindLineTarget);
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
