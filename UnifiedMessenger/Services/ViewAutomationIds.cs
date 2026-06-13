namespace UnifiedMessenger.Services;

/// <summary>
/// Stable AutomationId values consumed by UiSmoke, Narrator, and unit tests.
/// </summary>
public static class ViewAutomationIds
{
    public const string WorkspaceSidebar = "WorkspaceSidebar";
    public const string SidebarDashboard = "SidebarDashboard";
    public const string SidebarAddInstance = "SidebarAddInstance";
    public const string SidebarNotifications = "SidebarNotifications";
    public const string SidebarSettings = "SidebarSettings";

    public const string PersonalGlobalSearch = "PersonalGlobalSearch";
    public const string PersonalSummaryCards = "PersonalSummaryCards";
    public const string PersonalLayoutEditToggle = "PersonalLayoutEditToggle";
    public const string PersonalRecentActivity = "PersonalRecentActivity";
    public const string PersonalAccountStatus = "PersonalAccountStatus";

    public const string NotificationFeedPanel = "NotificationFeedPanel";
    public const string CommandPaletteRoot = "CommandPaletteRoot";
    public const string CommandPaletteSearch = "CommandPaletteSearch";

    public const string ShellNotificationToggle = "ShellNotificationToggle";

    public const string OccSnapshotReady = "OccSnapshotReady";

    public static string SidebarInstance(string instanceId) =>
        $"SidebarInstance_{Sanitize(instanceId)}";

    public static string PersonalMetric(string metricKey) =>
        $"PersonalMetric_{Sanitize(metricKey)}";

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var chars = value.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        return new string(chars);
    }
}
