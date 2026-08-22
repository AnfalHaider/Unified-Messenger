namespace UnifiedMessenger.Services;

internal static class WorkspaceSidebarAccessibility
{
    public static string ResolveRowAutomationId(string key) =>
        WorkspaceSidebarHelper.IsSelectionMatch(key, WorkspaceSidebarHelper.DashboardSelectionKey)
            ? ViewAutomationIds.SidebarDashboard
            : ViewAutomationIds.SidebarInstance(key);

    public static string ComposeDashboardName(bool selected) =>
        selected ? "Sidebar Dashboard, selected" : "Sidebar Dashboard";

    /// <param name="badgeCountsReviews">
    /// True for a Google Business account, where the badge is reviews awaiting a reply rather than unread
    /// messages.
    /// </param>
    public static string ComposeInstanceName(
        string displayName,
        string statusSubtitle,
        int badgeCount,
        bool selected,
        bool badgeCountsReviews = false)
    {
        var parts = new List<string> { displayName.Trim() };
        if (!string.IsNullOrWhiteSpace(statusSubtitle))
        {
            parts.Add(statusSubtitle.Trim());
        }

        if (badgeCount > 0)
        {
            // The same badge now carries two different meanings. Google Business has no unread messages —
            // that channel has no messaging at all — so announcing "6 unread" on a review count is simply
            // false to anyone who cannot see which kind of account the row is.
            parts.Add(badgeCountsReviews
                ? badgeCount == 1 ? "1 review awaiting a reply" : $"{badgeCount} reviews awaiting a reply"
                : badgeCount == 1 ? "1 unread" : $"{badgeCount} unread");
        }

        if (selected)
        {
            parts.Add("selected");
        }
        else
        {
            // Kept, but no longer load-bearing. This phrase was added when the rows were plain Borders that
            // exposed no automation pattern at all — a screen reader announced them as Groups, and baking
            // the action into the name was the only affordance available. `NavigationRow` now reports as a
            // Button and implements IInvokeProvider, so the control type already says it can be pressed and
            // assistive tech can activate it directly rather than only via Enter/Space.
            //
            // It stays because it costs one clause and still helps at verbosity settings that do not
            // announce control type, and because the location headers above these rows word themselves the
            // same way ("…, press to collapse or expand"). Skipped on the selected row, where it is noise.
            parts.Add("press to open");
        }

        return string.Join(", ", parts);
    }

    public static string ComposeSectionHeaderName(string title) =>
        $"{title.Trim()} section";
}
