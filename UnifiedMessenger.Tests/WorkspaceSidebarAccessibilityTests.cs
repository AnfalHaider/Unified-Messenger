using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WorkspaceSidebarAccessibilityTests
{
    [Fact]
    public void ComposeInstanceName_IncludesUnreadAndSelection()
    {
        var name = WorkspaceSidebarAccessibility.ComposeInstanceName(
            "Depilex DHA-2",
            "Connected",
            badgeCount: 3,
            selected: true);

        Assert.Contains("Depilex DHA-2", name, StringComparison.Ordinal);
        Assert.Contains("Connected", name, StringComparison.Ordinal);
        Assert.Contains("3 unread", name, StringComparison.Ordinal);
        Assert.Contains("selected", name, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRowAutomationId_UsesStableSidebarInstancePrefix()
    {
        var automationId = WorkspaceSidebarAccessibility.ResolveRowAutomationId("wa-dha-2");

        Assert.Equal(ViewAutomationIds.SidebarInstance("wa-dha-2"), automationId);
        Assert.StartsWith("SidebarInstance_", automationId, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeSectionHeaderName_AnnouncesSectionLandmark()
    {
        var name = WorkspaceSidebarAccessibility.ComposeSectionHeaderName("Pro / Business");

        Assert.Contains("section", name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pro / Business", name, StringComparison.Ordinal);
    }
}
