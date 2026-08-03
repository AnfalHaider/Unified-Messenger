using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WorkspaceSidebarHelperTests
{
    [Theory]
    [InlineData(ShellSection.Dashboard, null, WorkspaceSidebarHelper.DashboardSelectionKey)]
    [InlineData(ShellSection.Analytics, null, WorkspaceSidebarHelper.AnalyticsSelectionKey)]
    [InlineData(ShellSection.Reviews, null, WorkspaceSidebarHelper.ReviewsSelectionKey)]
    [InlineData(ShellSection.Reports, null, WorkspaceSidebarHelper.ReportsSelectionKey)]
    [InlineData(ShellSection.Settings, null, WorkspaceSidebarHelper.SettingsSelectionKey)]
    // An open account is what's actually on screen, so it wins over whichever section is loaded behind it.
    [InlineData(ShellSection.Dashboard, "  inst-whatsapp  ", "inst-whatsapp")]
    [InlineData(ShellSection.Analytics, "inst-1", "inst-1")]
    public void ResolveSelectionKey_PrefersAnOpenAccountOverTheSection(
        ShellSection section,
        string? instanceId,
        string expected)
    {
        Assert.Equal(expected, WorkspaceSidebarHelper.ResolveSelectionKey(section, instanceId));
    }

    [Fact]
    public void ResolveSelectionKey_NotificationDockOutranksBoth()
    {
        // The dock overlays the current destination rather than replacing it, so while it's open it is
        // the thing the sidebar should highlight.
        Assert.Equal(
            WorkspaceSidebarHelper.NotificationHubSelectionKey,
            WorkspaceSidebarHelper.ResolveSelectionKey(
                ShellSection.Analytics,
                "inst-1",
                notificationHubSelected: true));
    }

    [Fact]
    public void ParseSection_RoundTripsEverySection()
    {
        foreach (var section in Enum.GetValues<ShellSection>())
        {
            var key = WorkspaceSidebarHelper.SectionSelectionKey(section);
            Assert.Equal(section, WorkspaceSidebarHelper.ParseSection(key));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-section")]
    public void ParseSection_FallsBackToDashboardOnAnythingUnrecognised(string? key)
    {
        // This value is persisted to settings.json and can be hand-edited or written by an older build,
        // so it must never be able to stop the shell from opening.
        Assert.Equal(ShellSection.Dashboard, WorkspaceSidebarHelper.ParseSection(key));
    }

    [Fact]
    public void ParseSection_IsCaseAndWhitespaceInsensitive()
    {
        Assert.Equal(ShellSection.Reviews, WorkspaceSidebarHelper.ParseSection("  REVIEWS "));
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
            "Connected · syncing",
            WorkspaceSidebarHelper.ResolveStatusSubtitle(
                InstanceConnectionStatus.Connected,
                AdapterHealthState.Unknown,
                notificationsMuted: false));
    }

    [Fact]
    public void ResolveStatusSubtitle_ShowsConnectedWhenHandshakeSucceeds()
    {
        Assert.Equal(
            "Connected",
            WorkspaceSidebarHelper.ResolveStatusSubtitle(
                InstanceConnectionStatus.Connected,
                AdapterHealthState.Healthy,
                notificationsMuted: false));
    }

    [Fact]
    public void ResolveStatusSubtitle_KeepsSidebarCompactWhenDetailPresent()
    {
        Assert.Equal(
            "Connected",
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
    [InlineData(MemoryTierPreference.Low, "Low")]
    [InlineData(MemoryTierPreference.Normal, "Normal")]
    [InlineData(MemoryTierPreference.High, "High")]
    public void FormatMemoryTierLabel_ReturnsDisplayName(MemoryTierPreference tier, string expected)
    {
        Assert.Equal(expected, WorkspaceSidebarHelper.FormatMemoryTierLabel(tier));
    }

    [Theory]
    [InlineData(MemoryTierPreference.Normal, "Connected")]
    [InlineData(MemoryTierPreference.Low, "Connected · Memory: Low")]
    [InlineData(MemoryTierPreference.High, "Connected · Memory: High")]
    public void AppendMemoryTierHint_AppendsOnlyForNonNormalTiers(
        MemoryTierPreference tier,
        string expected)
    {
        Assert.Equal(
            expected,
            WorkspaceSidebarHelper.AppendMemoryTierHint("Connected", tier));
    }

    [Fact]
    public void ComposeInstanceTooltip_IncludesMemoryTier()
    {
        var tooltip = WorkspaceSidebarHelper.ComposeInstanceTooltip(
            "Sales WhatsApp",
            WorkspaceCategory.Professional,
            "Connected",
            "Adapter ready",
            MemoryTierPreference.High);

        Assert.Contains("Sales WhatsApp", tooltip, StringComparison.Ordinal);
        Assert.Contains("Memory tier: High", tooltip, StringComparison.Ordinal);
        Assert.Contains("Adapter: Adapter ready", tooltip, StringComparison.Ordinal);
    }
}
