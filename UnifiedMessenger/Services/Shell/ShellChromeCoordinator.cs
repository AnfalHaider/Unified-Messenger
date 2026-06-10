using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Shell;

public sealed class ShellChromeCoordinator
{
    private readonly IShellUiHost _ui;
    private readonly ApplicationServices _services;
    private readonly Func<ShellSelectionState> _getSelection;

    public bool PanePinned { get; set; }

    public bool SidebarHoverExpanded { get; set; }

    public bool NotificationPanelVisible { get; set; }

    public ShellChromeCoordinator(
        IShellUiHost ui,
        ApplicationServices services,
        Func<ShellSelectionState> getSelection)
    {
        _ui = ui;
        _services = services;
        _getSelection = getSelection;
    }

    public bool IsAppInForeground { get; set; } = true;

    public void UpdatePanePinUi(Button panePinButton, FontIcon panePinIcon)
    {
        if (PanePinned)
        {
            panePinIcon.Glyph = "\uE840";
            ToolTipService.SetToolTip(panePinButton, "Unpin sidebar (compact rail with hover expand)");
        }
        else
        {
            panePinIcon.Glyph = "\uE718";
            ToolTipService.SetToolTip(panePinButton, "Pin sidebar expanded");
        }
    }

    public void ApplySidebarLayout(bool forceVisible = false)
    {
        if (MainWindowShellLayout.ShouldUseCompactSidebarDisplay(_ui.SidebarColumn.Width.Value, forceVisible))
        {
            _ui.WorkspaceSidebar.SetCompactDisplay(true);
            return;
        }

        var width = MainWindowShellLayout.ResolveSidebarWidth(PanePinned, SidebarHoverExpanded);
        _ui.SidebarColumn.Width = new GridLength(width);
        _ui.WorkspaceSidebar.SetCompactDisplay(MainWindowShellLayout.IsCompactSidebarWidth(width));
    }

    public void RebuildInstanceNavigation()
    {
        _services.NotificationHub.SyncMutedInstances(_services.Registry.Instances);

        var selection = _getSelection();
        _ui.WorkspaceSidebar.Refresh(
            _services.Registry.Instances,
            selection.SelectedInstanceId,
            selection.IsDashboardSelected,
            selection.IsSettingsSelected,
            NotificationPanelVisible);

        foreach (var instance in _services.Registry.Instances)
        {
            UpdateInstanceBadge(instance.Id);
            _ui.WorkspaceSidebar.UpdateInstanceHealth(instance.Id, instance);
        }

        ApplySidebarLayout(forceVisible: _ui.SidebarColumn.Width.Value > 0);
    }

    public void UpdateInstanceBadge(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        var count = instance?.NotificationsMuted == true
            ? 0
            : _services.NotificationHub.GetBadgeCount(instanceId);
        _ui.WorkspaceSidebar.UpdateInstanceBadge(instanceId, count, instance);
    }

    public void UpdateShellChromeSelection()
    {
        var selection = _getSelection();
        _ui.WorkspaceSidebar.SetSelection(
            selection.IsDashboardSelected,
            selection.SelectedInstanceId,
            selection.IsSettingsSelected,
            NotificationPanelVisible);

        var notificationSelected = NotificationPanelVisible;
        _ui.NotificationToggleButton.Background = notificationSelected
            ? Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush
              ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        _ui.NotificationToggleButton.BorderThickness = notificationSelected
            ? new Thickness(0, 0, 0, 2)
            : new Thickness(0);
        _ui.NotificationToggleButton.BorderBrush = notificationSelected
            ? Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush
            : null;
    }

    public void SetNotificationPanelVisible(bool isVisible)
    {
        NotificationPanelVisible = isVisible;
        ApplyNotificationPanelVisibilityMetrics(isVisible);
        _ui.NotificationPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        UpdateShellChromeSelection();
    }

    public void ApplyNotificationPanelDockLayout()
    {
        var dock = _services.AppSettings.Settings.PanelDock;
        if (dock == NotificationPanelDock.Bottom)
        {
            Grid.SetColumn(_ui.NotificationPanel, 1);
            Grid.SetRow(_ui.NotificationPanel, 1);
            Grid.SetColumnSpan(_ui.NotificationPanel, 1);
            Grid.SetRowSpan(_ui.NotificationPanel, 1);
        }
        else
        {
            Grid.SetColumn(_ui.NotificationPanel, 2);
            Grid.SetRow(_ui.NotificationPanel, 0);
            Grid.SetColumnSpan(_ui.NotificationPanel, 1);
            Grid.SetRowSpan(_ui.NotificationPanel, 2);
        }

        ApplyNotificationPanelVisibilityMetrics(NotificationPanelVisible);
    }

    private void ApplyNotificationPanelVisibilityMetrics(bool isVisible)
    {
        var metrics = MainWindowShellLayout.ResolveNotificationPanelMetrics(
            _services.AppSettings.Settings.PanelDock,
            isVisible);
        _ui.NotificationColumn.Width = metrics.ColumnWidth;
        _ui.NotificationRow.Height = metrics.RowHeight;
    }
}
