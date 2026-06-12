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

        return string.Join(", ", parts);
    }

    public static string ComposeSectionHeaderName(string title) =>
        $"{title.Trim()} section";
}
