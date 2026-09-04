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
    // ---- Bands ------------------------------------------------------------------------------------
    //
    // TabIndex is scoped to the whole tab-navigation scope, NOT to the control tree a value is declared
    // in — so numbers chosen independently for the sidebar and for a page share one namespace and collide.
    // They did. The sidebar numbers every row it builds from SidebarMenuBase upward (1, 2, 3 …) and this
    // owner's rail carries eleven: four overview rows plus seven accounts. Settings' section nav was 10
    // and its content 20, so sidebar row ten and the Settings nav both claimed 10, and row twenty, the
    // Settings content and the personal search box all claimed 20. WinUI orders equal indices by tree
    // position, which is why tabbing off the Dashboard row landed on Reports rather than Analytics.
    //
    // Three bands, far enough apart that no realistic row count can bridge them, in the order a sighted
    // user reads the window: the left rail, then its footer, then the page content on the right.
    //
    //   1 – 89    sidebar rows          (SidebarRowCeiling guards the top)
    //   90 – 99   sidebar footer
    //   200 +     page content
    //
    // Keep every new constant inside a band. AccessibilityTabOrderTests fails the build on an overlap.

    public const int SidebarMenuBase = 1;

    /// <summary>
    /// The highest TabIndex a sidebar row may take before it would collide with the footer. Not a limit on
    /// how many accounts the product supports — the rail scrolls — but the point past which tab order stops
    /// being meaningful and starts being wrong.
    /// </summary>
    public const int SidebarRowCeiling = 89;

    public const int SidebarFooterAddInstance = 90;

    public const int SidebarFooterNotifications = 91;

    public const int SidebarFooterSettings = 92;

    /// <summary>First index of the page-content band. Everything on the right of the rail sits above this.</summary>
    public const int PageContentBase = 200;

    public const int SettingsSectionNav = PageContentBase;

    public const int SettingsContent = PageContentBase + 10;

    public const int PersonalOverviewList = PageContentBase + 15;

    public const int PersonalSearchBox = PageContentBase + 20;

    public static void ApplyTabIndex(Microsoft.UI.Xaml.Controls.Control control, int tabIndex) =>
        control.TabIndex = tabIndex;
}
