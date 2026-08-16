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
    public void AnUnselectedAccountSaysItCanBeOpened()
    {
        // F-A11Y-05. The row is a Border with a KeyDown handler, not a Button, so it exposes no Invoke
        // pattern and announces as a bare Group. Enter and Space do open it — only the affordance was
        // missing, and the location header immediately above already says "press to collapse or expand".
        // A tab-order walk of the live app put the two next to each other:
        //   [Group] 'DHA-2 location, 2 accounts, press to collapse or expand'
        //   [Group] 'Depilex DHA-2 WhatsApp, WhatsApp'          <- does nothing, as far as the user knows
        var name = WorkspaceSidebarAccessibility.ComposeInstanceName(
            "Depilex DHA-2", "Connected", badgeCount: 0, selected: false);

        Assert.Contains("press to open", name, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAlreadySelectedAccountDoesNotSayPressToOpen()
    {
        // Reading "press to open" on the account already showing is noise, and "selected" is the more
        // useful word in that position.
        var name = WorkspaceSidebarAccessibility.ComposeInstanceName(
            "Depilex DHA-2", "Connected", badgeCount: 0, selected: true);

        Assert.DoesNotContain("press to open", name, StringComparison.Ordinal);
        Assert.Contains("selected", name, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAffordanceComesAfterTheStatusSoTheImportantWordsAreHeardFirst()
    {
        // Screen-reader users routinely interrupt a long name. Name, then status, then unread count, then
        // the affordance last — so cutting it off early still leaves the useful part.
        var name = WorkspaceSidebarAccessibility.ComposeInstanceName(
            "Depilex DHA-2", "No internet — reconnecting…", badgeCount: 4, selected: false);

        var status = name.IndexOf("No internet", StringComparison.Ordinal);
        var unread = name.IndexOf("4 unread", StringComparison.Ordinal);
        var affordance = name.IndexOf("press to open", StringComparison.Ordinal);

        Assert.True(status < unread && unread < affordance, $"unexpected order: '{name}'");
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
