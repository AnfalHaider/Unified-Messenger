using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class MainWindowShellLayoutTests
{
    [Theory]
    [InlineData(true, false, MainWindowShellLayout.SidebarWidthExpanded)]
    [InlineData(false, true, MainWindowShellLayout.SidebarWidthExpanded)]
    [InlineData(false, false, MainWindowShellLayout.SidebarWidthCompact)]
    public void ResolveSidebarWidth_UsesPinOrHoverState(
        bool panePinned,
        bool hoverExpanded,
        double expectedWidth)
    {
        Assert.Equal(expectedWidth, MainWindowShellLayout.ResolveSidebarWidth(panePinned, hoverExpanded));
    }

    [Theory]
    [InlineData(NotificationPanelAutoOpenMode.Always, true, true)]
    [InlineData(NotificationPanelAutoOpenMode.Always, false, true)]
    [InlineData(NotificationPanelAutoOpenMode.Never, false, false)]
    [InlineData(NotificationPanelAutoOpenMode.UnfocusedOnly, true, false)]
    [InlineData(NotificationPanelAutoOpenMode.UnfocusedOnly, false, true)]
    public void ShouldAutoOpenNotificationPanel_RespectsMode(
        NotificationPanelAutoOpenMode mode,
        bool isAppInForeground,
        bool expected)
    {
        Assert.Equal(
            expected,
            MainWindowShellLayout.ShouldAutoOpenNotificationPanel(mode, isAppInForeground));
    }

    [Fact]
    public void ResolveNotificationPanelMetrics_UsesRightDockDimensions()
    {
        var metrics = MainWindowShellLayout.ResolveNotificationPanelMetrics(
            NotificationPanelDock.Right,
            isVisible: true);

        Assert.Equal(MainWindowShellLayout.NotificationPanelWidth, metrics.ColumnWidth.Value);
        Assert.Equal(0, metrics.RowHeight.Value);
    }

    [Fact]
    public void ResolveNotificationPanelMetrics_UsesBottomDockDimensions()
    {
        var metrics = MainWindowShellLayout.ResolveNotificationPanelMetrics(
            NotificationPanelDock.Bottom,
            isVisible: true);

        Assert.Equal(0, metrics.ColumnWidth.Value);
        Assert.Equal(MainWindowShellLayout.BottomNotificationPanelHeight, metrics.RowHeight.Value);
    }
}
