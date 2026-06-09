using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class WorkspaceSidebarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedKey = WorkspaceSidebarHelper.DashboardSelectionKey;

    [ObservableProperty]
    private int _notificationHubBadgeCount;

    public void ApplySelection(bool dashboardSelected, string? instanceId, bool settingsSelected) =>
        SelectedKey = WorkspaceSidebarHelper.ResolveSelectionKey(
            dashboardSelected,
            instanceId,
            settingsSelected);

    public void ApplyNotificationHubBadge(int totalUnread) =>
        NotificationHubBadgeCount = WorkspaceSidebarHelper.ClampBadgeCount(totalUnread);
}
