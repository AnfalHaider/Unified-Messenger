using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WorkspaceSidebarHelperTests
{
    [Theory]
    [InlineData(true, "inst-1", WorkspaceSidebarHelper.DashboardSelectionKey)]
    [InlineData(false, null, WorkspaceSidebarHelper.DashboardSelectionKey)]
    [InlineData(false, "  inst-whatsapp  ", "inst-whatsapp")]
    public void ResolveSelectionKey_NormalizesDashboardAndInstanceSelection(
        bool dashboardSelected,
        string? instanceId,
        string expected)
    {
        Assert.Equal(expected, WorkspaceSidebarHelper.ResolveSelectionKey(dashboardSelected, instanceId));
    }

    [Theory]
    [InlineData("inst-1", "inst-1", true)]
    [InlineData("INST-1", "inst-1", true)]
    [InlineData("inst-2", "inst-1", false)]
    public void IsSelectionMatch_IsCaseInsensitive(string selectedKey, string rowKey, bool expected)
    {
        Assert.Equal(expected, WorkspaceSidebarHelper.IsSelectionMatch(selectedKey, rowKey));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 12)]
    [InlineData(150, 99)]
    public void ClampBadgeCount_CapsDisplayValue(int count, int expected)
    {
        Assert.Equal(expected, WorkspaceSidebarHelper.ClampBadgeCount(count));
    }

    [Fact]
    public void PartitionInstances_GroupsByWorkspaceAndSortOrder()
    {
        var instances = new List<MessengerInstance>
        {
            new() { Id = "p-2", DisplayName = "Beta", SortOrder = 2, Category = WorkspaceCategory.Personal },
            new() { Id = "b-1", DisplayName = "Biz", SortOrder = 1, Category = WorkspaceCategory.Professional },
            new() { Id = "p-1", DisplayName = "Alpha", SortOrder = 1, Category = WorkspaceCategory.Personal },
            new() { Id = "   ", DisplayName = "Invalid" },
            new() { Id = "p-1", DisplayName = "Duplicate", SortOrder = 9, Category = WorkspaceCategory.Personal }
        };

        var (professional, personal) = WorkspaceSidebarHelper.PartitionInstances(instances);

        Assert.Equal(["b-1"], professional.Select(instance => instance.Id));
        Assert.Equal(["p-1", "p-2"], personal.Select(instance => instance.Id));
    }

    [Fact]
    public void ResolveStatusSubtitle_PrefersMutedLabel()
    {
        Assert.Equal(
            "Notifications muted",
            WorkspaceSidebarHelper.ResolveStatusSubtitle(
                InstanceConnectionStatus.Connected,
                AdapterHealthState.Healthy,
                notificationsMuted: true));
    }

    [Fact]
    public void ResolveStatusSubtitle_DoesNotShowUnknownForConnectedAdapter()
    {
        Assert.Equal(
            "Status: Connected · syncing",
            WorkspaceSidebarHelper.ResolveStatusSubtitle(
                InstanceConnectionStatus.Connected,
                AdapterHealthState.Unknown,
                notificationsMuted: false));
    }

    [Fact]
    public void ResolveStatusSubtitle_ShowsConnectedWhenHandshakeSucceeds()
    {
        Assert.Equal(
            "Status: Connected",
            WorkspaceSidebarHelper.ResolveStatusSubtitle(
                InstanceConnectionStatus.Connected,
                AdapterHealthState.Healthy,
                notificationsMuted: false));
    }

    [Fact]
    public void ResolveStatusSubtitle_ShowsAwaitingViewContextFromDetail()
    {
        Assert.Equal(
            "Status: Connected · awaiting view context",
            WorkspaceSidebarHelper.ResolveStatusSubtitle(
                InstanceConnectionStatus.Connected,
                AdapterHealthState.Healthy,
                notificationsMuted: false,
                connectionDetail: "Connected · awaiting view context"));
    }

    [Fact]
    public void FormatConnectedDetailSubtitle_PreservesStatusPrefix()
    {
        Assert.Equal(
            "Status: Connected · awaiting view context",
            WorkspaceSidebarHelper.FormatConnectedDetailSubtitle("Connected · awaiting view context"));
    }

    [Theory]
    [InlineData("inst-1", "inst-2", true)]
    [InlineData("inst-1", "inst-1", false)]
    [InlineData(" ", "inst-1", false)]
    public void ShouldAcceptReorder_ValidatesDistinctInstanceIds(
        string sourceId,
        string targetId,
        bool expected)
    {
        Assert.Equal(expected, WorkspaceSidebarHelper.ShouldAcceptReorder(sourceId, targetId));
    }

    [Theory]
    [InlineData(MemoryTierPreference.Low, "Low")]
    [InlineData(MemoryTierPreference.Normal, "Normal")]
    [InlineData(MemoryTierPreference.High, "High")]
    public void FormatMemoryTierLabel_ReturnsDisplayName(MemoryTierPreference tier, string expected)
    {
        Assert.Equal(expected, WorkspaceSidebarHelper.FormatMemoryTierLabel(tier));
    }

    [Theory]
    [InlineData(MemoryTierPreference.Normal, "Status: Connected")]
    [InlineData(MemoryTierPreference.Low, "Status: Connected · Memory: Low")]
    [InlineData(MemoryTierPreference.High, "Status: Connected · Memory: High")]
    public void AppendMemoryTierHint_AppendsOnlyForNonNormalTiers(
        MemoryTierPreference tier,
        string expected)
    {
        Assert.Equal(
            expected,
            WorkspaceSidebarHelper.AppendMemoryTierHint("Status: Connected", tier));
    }

    [Fact]
    public void ComposeInstanceTooltip_IncludesMemoryTier()
    {
        var tooltip = WorkspaceSidebarHelper.ComposeInstanceTooltip(
            "Sales WhatsApp",
            WorkspaceCategory.Professional,
            "Status: Connected",
            "Adapter ready",
            MemoryTierPreference.High);

        Assert.Contains("Sales WhatsApp", tooltip, StringComparison.Ordinal);
        Assert.Contains("Memory tier: High", tooltip, StringComparison.Ordinal);
        Assert.Contains("Adapter: Adapter ready", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDropTargetInstanceId_MatchesRowBounds()
    {
        var bounds = new List<SidebarRowBounds>
        {
            new("inst-a", 0, 40),
            new("inst-b", 41, 80)
        };

        Assert.Equal("inst-a", WorkspaceSidebarHelper.ResolveDropTargetInstanceId(20, bounds));
        Assert.Equal("inst-b", WorkspaceSidebarHelper.ResolveDropTargetInstanceId(60, bounds));
        Assert.Null(WorkspaceSidebarHelper.ResolveDropTargetInstanceId(200, bounds));
    }
}
