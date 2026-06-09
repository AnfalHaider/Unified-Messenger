using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Tests;

public class Wave8OccTests
{
    [Fact]
    public void OccThreadCardPresenter_FiltersKanbanColumnThreads()
    {
        var threads = new[]
        {
            new ThreadData
            {
                ThreadId = "t1",
                Platform = "whatsapp",
                InstanceId = "inst-1",
                CustomerName = "Alex"
            },
            new ThreadData
            {
                ThreadId = "t2",
                Platform = "whatsapp",
                InstanceId = "inst-1",
                IsRevenueLeakageRisk = true,
                CustomerName = "Sam"
            }
        };

        var newInquiries = OccThreadCardPresenter.FilterKanbanColumn(
            threads,
            UnifiedMessengerKanbanColumn.NewInquiries);
        var hangingLeads = OccThreadCardPresenter.FilterKanbanColumn(
            threads,
            UnifiedMessengerKanbanColumn.HangingLeads);

        Assert.Single(newInquiries);
        Assert.Equal("Alex", newInquiries[0].CustomerName);
        Assert.Single(hangingLeads);
        Assert.Equal("Sam", hangingLeads[0].CustomerName);
    }

    [Fact]
    public void OccBackfillStatusPresenter_DetectsRunningAccounts()
    {
        var manager = BackfillSyncManager.Instance;
        manager.SetStateForTests("inst-a", BackfillSyncState.Running);
        manager.SetStateForTests("inst-b", BackfillSyncState.Completed);

        try
        {
            var status = OccBackfillStatusPresenter.BuildStatus(
            [
                new MessengerInstance
                {
                    Id = "inst-a",
                    DisplayName = "Sales",
                    ProfileName = "sales",
                    Platform = "whatsapp"
                },
                new MessengerInstance
                {
                    Id = "inst-b",
                    DisplayName = "Support",
                    ProfileName = "support",
                    Platform = "whatsapp"
                }
            ]);

            Assert.True(status.IsRunning);
            Assert.Equal(1, status.RunningCount);
            Assert.True(status.ShowStatus);
            Assert.Contains("1 account", status.StatusText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            manager.SetStateForTests("inst-a", BackfillSyncState.NotStarted);
            manager.SetStateForTests("inst-b", BackfillSyncState.NotStarted);
        }
    }

    [Fact]
    public void OperationsCommandCenterViewModel_AppliesShellAndQueuePresentation()
    {
        var vm = new OperationsCommandCenterViewModel();
        vm.ApplyShellPresentation(new OccShellPresentation
        {
            ShowEmptyState = false,
            ShowMainContent = true,
            ScopeLabel = "Showing: Downtown",
            LastRefreshedText = "Updated 2:30 PM"
        });
        vm.ApplyImmediateQueuePresentation(new OccImmediateQueuePresentation
        {
            ShowEmptyState = false,
            ShowFooter = true,
            FooterText = "Showing top 3 of 9 urgent threads"
        });

        Assert.Equal("Showing: Downtown", vm.ScopeLabel);
        Assert.True(vm.ShowImmediateQueueFooter);
        Assert.Equal("Showing top 3 of 9 urgent threads", vm.ImmediateQueueFooterText);
    }

    [Fact]
    public void BranchWorkspacePillBarViewModel_AppliesPillBarItems()
    {
        var vm = new BranchWorkspacePillBarViewModel();
        var pillBar = OccSnapshotPresenter.BuildPillBar(
            new Dictionary<string, BranchWorkspaceHelper.BranchTabCounts>(StringComparer.OrdinalIgnoreCase)
            {
                ["Downtown"] = new(2, 1)
            },
            ["Downtown"]);

        vm.ApplyPillBar(pillBar, "Downtown");

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("Downtown", vm.SelectedBranchKey);
        Assert.Equal(pillBar.Signature, vm.PillBarSignature);
    }

    [Fact]
    public void OccLayoutCommandHelper_MovesPanelWithinOrder()
    {
        var settings = new AppSettings();
        var order = new List<string> { "A", "B", "C" };

        OccLayoutCommandHelper.MovePanel(
            settings,
            order,
            "C",
            "A",
            (_, working) => order = working);

        Assert.Equal(["C", "A", "B"], order);
    }
}
