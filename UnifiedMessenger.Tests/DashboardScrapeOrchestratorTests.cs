using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardScrapeOrchestratorTests
{
    [Fact]
    public void IsDashboardScrapeCapable_OnlyMetaAndGoogleBusiness()
    {
        Assert.True(DashboardScrapeOrchestrator.IsDashboardScrapeCapable(
            new MessengerInstance { Id = "m1", Platform = "metabusiness" }));
        Assert.True(DashboardScrapeOrchestrator.IsDashboardScrapeCapable(
            new MessengerInstance { Id = "g1", Platform = "googlebusiness" }));
        Assert.False(DashboardScrapeOrchestrator.IsDashboardScrapeCapable(
            new MessengerInstance { Id = "w1", Platform = "whatsapp" }));
    }

    [Fact]
    public void BuildForceScrapeScript_InvokesDashboardScrapeHooks()
    {
        var script = DashboardScrapeOrchestrator.BuildForceScrapeScript(new MessengerInstance
        {
            Id = "branch-1",
            Platform = "metabusiness"
        });

        Assert.Contains("__umForceDashboardScrape", script, StringComparison.Ordinal);
        Assert.Contains("branch-1", script, StringComparison.Ordinal);
        Assert.Contains("metabusiness", script, StringComparison.Ordinal);
    }
}
