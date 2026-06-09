using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Tests;

public class Wave11ValidationTests
{
    [Fact]
    public void PerformanceValidation_CoalescesTriageBurstToSingleRefresh()
    {
        Assert.Equal(1, PerformanceValidationHelper.EstimateCoalescedRefreshCount(12));
        Assert.Equal(0, PerformanceValidationHelper.EstimateCoalescedRefreshCount(0));
    }

    [Theory]
    [InlineData(StartupWarmMode.WarmAll, 5, false, 5)]
    [InlineData(StartupWarmMode.VisibleOnly, 5, false, 1)]
    [InlineData(StartupWarmMode.Lazy, 5, false, 1)]
    [InlineData(StartupWarmMode.WarmAll, 5, true, 1)]
    public void PerformanceValidation_EstimatesStartupWarmTargets(
        StartupWarmMode mode,
        int instanceCount,
        bool lazyLoading,
        int expectedTargets)
    {
        Assert.Equal(
            expectedTargets,
            PerformanceValidationHelper.EstimateStartupWarmTargets(mode, instanceCount, lazyLoading));
    }

    [Fact]
    public void PerformanceValidation_VisibleOnlyFasterThanWarmAllForFiveInstances()
    {
        Assert.True(PerformanceValidationHelper.LazyWarmAllIsFasterThanWarmAll(5));

        var warmAll = PerformanceValidationHelper.EstimateStartupWarmDuration(
            StartupWarmMode.WarmAll,
            5,
            enableLazyWebViewLoading: false);
        var visibleOnly = PerformanceValidationHelper.EstimateStartupWarmDuration(
            StartupWarmMode.VisibleOnly,
            5,
            enableLazyWebViewLoading: false);

        Assert.True(visibleOnly < warmAll);
    }

    [Fact]
    public void PerformanceValidation_AcceptsSubTwoSecondInstanceSwitch()
    {
        Assert.True(PerformanceValidationHelper.IsAcceptableInstanceSwitchLatency(TimeSpan.FromMilliseconds(900)));
        Assert.False(PerformanceValidationHelper.IsAcceptableInstanceSwitchLatency(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void DashboardRefreshCoordinator_RequestImmediateRefresh_FiresOncePerCall()
    {
        var coordinator = DashboardRefreshCoordinator.CreateForTests();
        var refreshCount = 0;
        coordinator.RefreshRequested += (_, _) => refreshCount++;

        coordinator.RequestImmediateRefresh();
        coordinator.RequestImmediateRefresh();

        Assert.Equal(2, refreshCount);
    }

    [Fact]
    public void DashboardRefreshCoordinator_Subscribe_AllowsBalancedUnsubscribe()
    {
        var coordinator = DashboardRefreshCoordinator.CreateForTests();

        coordinator.Subscribe();
        coordinator.Subscribe();
        coordinator.Unsubscribe();
        coordinator.Unsubscribe();

        var refreshCount = 0;
        coordinator.RefreshRequested += (_, _) => refreshCount++;
        coordinator.RequestImmediateRefresh();

        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public void DashboardRefreshCoordinator_UsesConfiguredDebounceWindow()
    {
        Assert.Equal(450, PerformanceValidationHelper.DashboardRefreshDebounceMilliseconds);
    }

    [Fact]
    public async Task ImportInstancesAsync_RejectsEmptyStore()
    {
        var tempDirectory = CreateTempDirectory();
        var storePath = Path.Combine(tempDirectory, "instances.json");
        var importPath = Path.Combine(tempDirectory, "empty.json");

        try
        {
            await File.WriteAllTextAsync(importPath, JsonSerializer.Serialize(new InstanceStore()));

            var registry = new InstanceRegistryService(storePath);
            await Assert.ThrowsAsync<InvalidDataException>(() => registry.ImportInstancesAsync(importPath));
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public async Task ImportInstancesAsync_RejectsMissingFile()
    {
        var tempDirectory = CreateTempDirectory();
        var storePath = Path.Combine(tempDirectory, "instances.json");

        try
        {
            var registry = new InstanceRegistryService(storePath);
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                registry.ImportInstancesAsync(Path.Combine(tempDirectory, "missing.json")));
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void SecurityAuditChecklist_ReportsAllItemsResolved()
    {
        Assert.Equal(10, SecurityAuditChecklist.Items.Count);
        Assert.Equal(10, SecurityAuditChecklist.ResolvedHighSeverityCount);
        Assert.All(SecurityAuditChecklist.Items, item => Assert.True(item.IsResolved));
    }

    [Fact]
    public void SecurityAudit_Revalidation_FindingsMitigatedInSource()
    {
        var repoRoot = FindRepoRoot();
        var platformAdapters = File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Services", "Adapters", "PlatformAdapters.cs"));
        var registry = File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Services", "InstanceRegistryService.cs"));
        var updateService = File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Services", "GitHubUpdateService.cs"));
        var operationalClear = File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Services", "OperationalDataService.cs"));
        var adapterCore = File.ReadAllText(Path.Combine(repoRoot, "UnifiedMessenger", "Assets", "Scripts", "adapter-core.js"));

        Assert.Contains("JsonSerializer.Serialize(instance.Id)", platformAdapters, StringComparison.Ordinal);
        Assert.Contains("ValidateInstanceStartUrls", registry, StringComparison.Ordinal);
        Assert.Contains("ResolveExpectedSha256", updateService, StringComparison.Ordinal);
        Assert.Contains("PromptBeforeAutoUpdate", updateService, StringComparison.Ordinal);
        Assert.Contains("NotificationHub.Instance.ClearAlerts", operationalClear, StringComparison.Ordinal);
        Assert.Contains("CSS.escape", adapterCore, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repoRoot, "UnifiedMessenger", "Services", "IWebViewScriptGateway.cs")));
    }

    [Fact]
    public void SecurityAudit_DocumentsVulnerablePackageScanCommand()
    {
        Assert.Contains(
            "dotnet list package --vulnerable",
            SecurityAuditChecklist.VulnerablePackageScanCommand,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UxValidationChecklist_CoversPrimaryXamlSurfaces()
    {
        Assert.True(UxValidationChecklist.XamlSurfaces.Count >= 20);
        Assert.Contains(UxValidationChecklist.XamlSurfaces, surface => surface.Contains("DashboardPage", StringComparison.Ordinal));
        Assert.Contains(UxValidationChecklist.XamlSurfaces, surface => surface.Contains("SettingsPage", StringComparison.Ordinal));
        Assert.Contains(UxValidationChecklist.XamlSurfaces, surface => surface.Contains("OperationsCommandCenter", StringComparison.Ordinal));
    }

    [Fact]
    public void UxValidationChecklist_IncludesKeyboardNavigationPaths()
    {
        Assert.Contains(
            UxValidationChecklist.KeyboardPaths,
            path => path.Contains("Branch workspace pill", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            UxValidationChecklist.KeyboardPaths,
            path => path.Contains("conversation key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChartViewModels_RenderEmptyStateWithoutThrowing()
    {
        var weekly = new WeeklyActivityChartViewModel();
        weekly.ApplySeries([]);

        var sentiment = new SentimentActivityChartViewModel();
        sentiment.ApplySeries(MessageTriageDashboardSnapshot.Empty);

        Assert.True(weekly.ShowEmptyHint);
        Assert.True(sentiment.ShowEmptyHint);
        Assert.Empty(weekly.Bars);
        Assert.Empty(sentiment.Bars);
    }

    [Fact]
    public void NotificationNavigationHelper_OpenAlert_FallsBackToInstanceOnly()
    {
        var navigation = ShellNavigationService.CreateForTests();
        InstanceNavigationRequest? request = null;
        navigation.InstanceNavigationRequested += (_, item) => request = item;

        NotificationNavigationHelper.OpenAlert(
            navigation,
            NotificationAlert.Create("inst-1", "Work", "slack", "Ping"));

        Assert.NotNull(request);
        Assert.Equal("inst-1", request!.InstanceId);
        Assert.False(request.HasConversationTarget);
    }

    [Fact]
    public void SettingsImportExportPresenter_BuildsImportSummaryFromStore()
    {
        var summary = SettingsImportExportPresenter.BuildImportSummary(
            @"C:\import\instances.json",
            new InstanceStore
            {
                Instances = [new MessengerInstance { Id = "a" }],
                ArchivedInstances = [new MessengerInstance { Id = "b" }, new MessengerInstance { Id = "c" }]
            });

        Assert.Equal(1, summary.ActiveCount);
        Assert.Equal(2, summary.ArchivedCount);
        Assert.Contains("instances.json", summary.SourcePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wave11Infra_UiSmokeJobRunsAfterPackageArtifacts()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "build.yml"));
        Assert.Contains("ui-smoke:", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: package", workflow, StringComparison.Ordinal);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
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
