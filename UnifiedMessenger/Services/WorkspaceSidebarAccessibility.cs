namespace UnifiedMessenger.Services;

internal static class WorkspaceSidebarAccessibility
{
    public static string ResolveRowAutomationId(string key) =>
        WorkspaceSidebarHelper.IsSelectionMatch(key, WorkspaceSidebarHelper.DashboardSelectionKey)
            ? ViewAutomationIds.SidebarDashboard
            : ViewAutomationIds.SidebarInstance(key);

    public static string ComposeDashboardName(bool selected) =>
        selected ? "Sidebar Dashboard, selected" : "Sidebar Dashboard";

    public static string ComposeInstanceName(
        string displayName,
        string statusSubtitle,
        int badgeCount,
        bool selected)
    {
        var parts = new List<string> { displayName.Trim() };
        if (!string.IsNullOrWhiteSpace(statusSubtitle))
        {
            parts.Add(statusSubtitle.Trim());
        }

        if (badgeCount > 0)
        {
            parts.Add(badgeCount == 1 ? "1 unread" : $"{badgeCount} unread");
        }

        if (selected)
        {
            parts.Add("selected");
        }
        else
        {
            // The row is a Border with a KeyDown handler, not a Button, so it exposes no Invoke pattern —
            // a screen reader announces it as a plain Group and gives no hint that it does anything.
            // Enter and Space DO open the account (InstanceRow_KeyDown), so the capability is there and
            // only the affordance was missing.
            //
            // The location headers directly above these rows already solve it the same way, by baking the
            // action into the name ("…, press to collapse or expand"). A tab-order walk of the live app
            // put the two side by side: the header said what it did, the account under it did not.
            // Skipped when the row is already the selected one, where "press to open" is just noise.
            parts.Add("press to open");
        }

        return string.Join(", ", parts);
    }

    public static string ComposeSectionHeaderName(string title) =>
        $"{title.Trim()} section";
}
