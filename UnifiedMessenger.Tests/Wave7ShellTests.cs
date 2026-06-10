using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Tests;

public class Wave7ShellTests
{
    [Fact]
    public void WorkspaceSidebarMenuPlanner_BuildsStableStructureKeys()
    {
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(
        [
            new MessengerInstance
            {
                Id = "pro-1",
                DisplayName = "Sales",
                ProfileName = "sales",
                Platform = "whatsapp",
                Category = WorkspaceCategory.Professional,
                SortOrder = 1
            },
            new MessengerInstance
            {
                Id = "personal-1",
                DisplayName = "Family",
                ProfileName = "family",
                Platform = "telegram",
                Category = WorkspaceCategory.Personal,
                SortOrder = 1
            }
        ]);

        Assert.Equal(6, plan.Entries.Count);
        Assert.Equal(WorkspaceSidebarHelper.DashboardSelectionKey, plan.Entries[1].Key);
        Assert.Equal("pro-1", plan.Entries[3].Key);
        Assert.Equal("personal-1", plan.Entries[5].Key);

        Assert.Equal("Overview", plan.Entries[0].SectionTitle);
        Assert.Equal("Pro / Business", plan.Entries[2].SectionTitle);
        Assert.Equal("Personal / Life", plan.Entries[4].SectionTitle);
    }

    [Fact]
    public void WorkspaceSidebarMenuPlanner_DetectsStructureChanges()
    {
        var initial = WorkspaceSidebarMenuPlanner.BuildPlan(
        [
            new MessengerInstance
            {
                Id = "a",
                DisplayName = "A",
                ProfileName = "a",
                Platform = "whatsapp",
                Category = WorkspaceCategory.Professional,
                SortOrder = 1
            }
        ]);

        var renamedOrder = WorkspaceSidebarMenuPlanner.BuildPlan(
        [
            new MessengerInstance
            {
                Id = "b",
                DisplayName = "B",
                ProfileName = "b",
                Platform = "whatsapp",
                Category = WorkspaceCategory.Professional,
                SortOrder = 1
            }
        ]);

        Assert.True(WorkspaceSidebarMenuPlanner.HasSameStructure(initial, initial));
        Assert.False(WorkspaceSidebarMenuPlanner.HasSameStructure(initial, renamedOrder));
    }

    [Fact]
    public void NotificationFeedPresenter_ShowsEmptyStateWhenNoAlerts()
    {
        var hub = NotificationHub.CreateForTests();
        var presentation = NotificationFeedPresenter.BuildPresentation(hub);

        Assert.False(presentation.ShowAlertList);
        Assert.False(presentation.ClearAllEnabled);
        Assert.False(presentation.MarkAllReadEnabled);
        Assert.Empty(presentation.FeedItems);
    }

    [Fact]
    public void NotificationFeedPanelHelper_EnablesCommandsWhenUnreadAlertsExist()
    {
        var states = NotificationFeedPanelHelper.ResolveCommandStates(totalAlertCount: 2, unreadAlertCount: 1);
        Assert.True(states.ClearEnabled);
        Assert.True(states.MarkAllReadEnabled);
    }

    [Fact]
    public void MainWindowViewModel_ReportsStartupWarmProgress()
    {
        var vm = new MainWindowViewModel();
        vm.BeginStartupWarm(3);
        vm.ReportStartupWarmProgress(1, 3, "Sales WhatsApp");

        Assert.True(vm.ShowStartupWarmProgress);
        Assert.Equal(1, vm.StartupWarmCompleted);
        Assert.Contains("Sales WhatsApp", vm.StartupWarmStatusText, StringComparison.Ordinal);
        Assert.Equal(1d / 3d, vm.StartupWarmProgress, precision: 3);
    }

    [Fact]
    public void WorkspaceSidebarViewModel_AppliesSelectionAndBadge()
    {
        var vm = new WorkspaceSidebarViewModel();
        vm.ApplySelection(dashboardSelected: false, instanceId: "inst-1", settingsSelected: true);
        vm.ApplyNotificationHubBadge(120);

        Assert.Equal(WorkspaceSidebarHelper.SettingsSelectionKey, vm.SelectedKey);
        Assert.Equal(99, vm.NotificationHubBadgeCount);
    }
}
