namespace UnifiedMessenger.Services;

/// <summary>
/// Tab order constants for keyboard navigation across primary surfaces.
/// </summary>
/// <remarks>
/// Four constants were deleted here because nothing applied them: <c>DashboardTabs</c>,
/// <c>OccRefreshButton</c>, <c>OccBranchPillBar</c> and <c>OccLayoutButton</c> all named the standalone
/// Operations Command Center, which was retired in v4.27.0. A constant no call site uses is not a tab
/// order — it is a claim that one exists.
/// <para>
/// The sidebar footer values below are duplicated as literals in <c>WorkspaceSidebar.xaml</c>, and they
/// had drifted: the XAML read 90/92/93, so Notifications carried Settings' number and Settings carried one
/// that appears nowhere here. Keep the two in step, or move the XAML onto these constants.
/// </para>
/// </remarks>
public static class AccessibilityTabOrderHelper
{
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
