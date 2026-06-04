using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class MainWindowShellLayout
{
    public const double SidebarWidthExpanded = 320;

    public const double SidebarWidthCompact = 56;

    public const double NotificationPanelWidth = 320;

    public const double BottomNotificationPanelHeight = 240;

    public static double ResolveSidebarWidth(bool panePinned, bool hoverExpanded) =>
        panePinned || hoverExpanded ? SidebarWidthExpanded : SidebarWidthCompact;

    public static bool ShouldUseCompactSidebarDisplay(double sidebarColumnWidth, bool forceVisible) =>
        sidebarColumnWidth <= 0 && !forceVisible;

    public static bool IsCompactSidebarWidth(double width) =>
        width <= SidebarWidthCompact + 1;

    public static bool IsAppInForeground(bool isWindowVisible, bool isWindowActivated) =>
        isWindowVisible && isWindowActivated;

    public static bool ShouldAutoOpenNotificationPanel(
        NotificationPanelAutoOpenMode mode,
        bool isAppInForeground) =>
        mode switch
        {
            NotificationPanelAutoOpenMode.Always => true,
            NotificationPanelAutoOpenMode.Never => false,
            _ => !isAppInForeground
        };

    public static (GridLength ColumnWidth, GridLength RowHeight) ResolveNotificationPanelMetrics(
        NotificationPanelDock dock,
        bool isVisible)
    {
        if (dock == NotificationPanelDock.Bottom)
        {
            return (
                new GridLength(0),
                isVisible ? new GridLength(BottomNotificationPanelHeight) : new GridLength(0));
        }

        return (
            isVisible ? new GridLength(NotificationPanelWidth) : new GridLength(0),
            new GridLength(0));
    }
}
