namespace UnifiedMessenger.Services;

/// <summary>
/// Tab order constants for keyboard navigation across primary surfaces.
/// </summary>
public static class AccessibilityTabOrderHelper
{
    public const int DashboardTabs = 10;

    public const int OccRefreshButton = 20;

    public const int OccBranchPillBar = 30;

    public const int OccLayoutButton = 40;

    public const int PersonalSearchBox = 20;

    public const int SidebarMenuBase = 1;

    public const int SidebarFooterAddInstance = 90;

    public const int SidebarFooterNotifications = 91;

    public const int SidebarFooterSettings = 92;

    public const int SettingsSectionNav = 10;

    public const int SettingsContent = 20;

    public static void ApplyTabIndex(Microsoft.UI.Xaml.Controls.Control control, int tabIndex) =>
        control.TabIndex = tabIndex;
}
