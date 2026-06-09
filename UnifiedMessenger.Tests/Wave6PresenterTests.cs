using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class Wave6PresenterTests
{
    [Fact]
    public void BuildStatusKpis_MapsOperationalMetrics()
    {
        var kpis = OccSnapshotPresenter.BuildStatusKpis(new OperationsStatusSnapshot
        {
            OpenThreadCount = 12,
            HangingLeadCount = 4,
            ImmediateActionCount = 7,
            ImmediateActionQueueCount = 5,
            TotalRevenueAtRisk = 1500,
            AverageReplyTime = "18m",
            AverageReplyTimeSubtext = "Across 6 replies",
            SlaBreaches = "2",
            SlaThresholdSubtext = "15 min threshold",
            ResponseRate = "82%",
            PeakHour = "2 PM",
            DailyTrend = "+12%"
        });

        Assert.Equal("12", kpis.OpenThreadCount);
        Assert.Equal("4", kpis.HangingLeadCount);
        Assert.Equal("PKR 1,500", kpis.RevenueAtRisk);
        Assert.Equal("7", kpis.ImmediateActionCount);
        Assert.Equal("18m", kpis.AverageReplyTime);
        Assert.Equal("2", kpis.SlaBreaches);
        Assert.Contains("top 5", kpis.ImmediateActionTooltip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildShellPresentation_ShowsEmptyStateWithoutProfessionalAccounts()
    {
        var shell = OccSnapshotPresenter.BuildShellPresentation(
            hasProfessionalInstances: false,
            scopeLabel: "Showing: All Branches",
            refreshedAtLocal: new DateTime(2026, 6, 3, 14, 30, 0));

        Assert.True(shell.ShowEmptyState);
        Assert.False(shell.ShowMainContent);
        Assert.Equal("Showing: All Branches", shell.ScopeLabel);
        Assert.Contains("Updated", shell.LastRefreshedText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPillBar_IncludesAllBranchesAndBranchCounts()
    {
        var counts = new Dictionary<string, BranchWorkspaceHelper.BranchTabCounts>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downtown"] = new(3, 1),
            ["Airport"] = new(2, 0)
        };

        var pillBar = OccSnapshotPresenter.BuildPillBar(counts, ["Downtown", "Airport"]);

        Assert.Equal(3, pillBar.Items.Count);
        Assert.Equal("All branches", pillBar.Items[0].BranchLabel);
        Assert.Equal(5, pillBar.Items[0].OpenCount);
        Assert.Equal(1, pillBar.Items[0].UrgentCount);
        Assert.Equal("Downtown", pillBar.Items[1].BranchKey);
        Assert.Equal(3, pillBar.Items[1].OpenCount);
        Assert.Equal(1, pillBar.Items[1].UrgentCount);
        Assert.Contains("all:5:1", pillBar.Signature, StringComparison.Ordinal);
        Assert.Contains("Downtown:3:1", pillBar.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildImmediateQueuePresentation_ShowsFooterWhenCapped()
    {
        var presentation = OccSnapshotPresenter.BuildImmediateQueuePresentation(new UnifiedMessengerDashboardSnapshot
        {
            ImmediateActionQueue =
            [
                new ThreadData
                {
                    ThreadId = "t1",
                    Platform = "whatsapp",
                    InstanceId = "inst-1"
                }
            ],
            ImmediateActionCount = 9,
            ImmediateActionQueueCount = 1
        });

        Assert.False(presentation.ShowEmptyState);
        Assert.True(presentation.ShowFooter);
        Assert.Equal("Showing top 1 of 9 urgent threads", presentation.FooterText);
    }

    [Fact]
    public void PersonalSnapshotPresenter_BuildViewState_ShowsNoAccountsEmptyState()
    {
        var viewState = PersonalSnapshotPresenter.BuildViewState(new PersonalDashboardSnapshot
        {
            EmptyReason = PersonalDashboardEmptyReason.NoPersonalAccounts
        });

        Assert.True(viewState.ShowNoAccountsEmptyState);
        Assert.True(viewState.ShowInstanceTilesEmptyState);
        Assert.Contains("Add Instance", viewState.InstanceTilesEmptyHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalAiModelRowViewModel_UsesToolkitDownloadButtonText()
    {
        var row = new UnifiedMessenger.ViewModels.LocalAiModelRowViewModel(
            "llama3",
            "Llama 3",
            "4.7 GB",
            "General purpose");

        Assert.Equal("Download", row.DownloadButtonText);

        row.IsDownloading = true;
        Assert.Equal("Downloading…", row.DownloadButtonText);

        row.IsDownloading = false;
        row.IsInstalled = true;
        Assert.Equal("Installed", row.DownloadButtonText);
    }
}
