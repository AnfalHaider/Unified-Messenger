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
    [InlineData(MainWindowShellLayout.SidebarWidthCompact, true)]
    [InlineData(MainWindowShellLayout.SidebarWidthExpanded, false)]
    [InlineData(0, true)]
    [InlineData(MainWindowShellLayout.SidebarWidthCompact + 1, true)]
    [InlineData(MainWindowShellLayout.SidebarWidthCompact + 2, false)]
    public void IsCompactSidebarWidth_UsesCompactRailThreshold(double width, bool expected)
    {
        Assert.Equal(expected, MainWindowShellLayout.IsCompactSidebarWidth(width));
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(0, true, false)]
    [InlineData(MainWindowShellLayout.SidebarWidthCompact, false, false)]
    [InlineData(MainWindowShellLayout.SidebarWidthExpanded, false, false)]
    public void ShouldUseCompactSidebarDisplay_RequiresHiddenColumnUnlessForced(
        double sidebarColumnWidth,
        bool forceVisible,
        bool expected)
    {
        Assert.Equal(
            expected,
            MainWindowShellLayout.ShouldUseCompactSidebarDisplay(sidebarColumnWidth, forceVisible));
    }

    [Fact]
    public void SidebarColumnWidths_UseExpectedRailAndExpandedValues()
    {
        Assert.Equal(56, MainWindowShellLayout.SidebarWidthCompact);
        Assert.Equal(320, MainWindowShellLayout.SidebarWidthExpanded);
        Assert.True(MainWindowShellLayout.SidebarWidthExpanded > MainWindowShellLayout.SidebarWidthCompact);
    }

    [Theory]
    [InlineData(NotificationPanelAutoOpenMode.Always, true, true)]
    [InlineData(NotificationPanelAutoOpenMode.Always, false, true)]
    [InlineData(NotificationPanelAutoOpenMode.Never, true, false)]
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

    [Theory]
    [InlineData(NotificationPanelAutoOpenMode.Always, true)]
    [InlineData(NotificationPanelAutoOpenMode.UnfocusedOnly, true)]
    [InlineData(NotificationPanelAutoOpenMode.Never, false)]
    public void ShouldQueueDeferredPanelReveal_RespectsNeverMode(
        NotificationPanelAutoOpenMode mode,
        bool expected)
    {
        Assert.Equal(expected, MainWindowShellLayout.ShouldQueueDeferredPanelReveal(mode));
    }

    [Theory]
    [InlineData(NotificationPanelAutoOpenMode.Always, true)]
    [InlineData(NotificationPanelAutoOpenMode.UnfocusedOnly, true)]
    [InlineData(NotificationPanelAutoOpenMode.Never, false)]
    public void ShouldRevealDeferredPanel_RespectsNeverMode(
        NotificationPanelAutoOpenMode mode,
        bool expected)
    {
        Assert.Equal(expected, MainWindowShellLayout.ShouldRevealDeferredPanel(mode));
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
