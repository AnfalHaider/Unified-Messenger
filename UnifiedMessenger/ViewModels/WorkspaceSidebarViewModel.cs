using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class WorkspaceSidebarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedKey = WorkspaceSidebarHelper.DashboardSelectionKey;

    [ObservableProperty]
    private int _notificationHubBadgeCount;

    public void ApplySelection(
        ShellSection section,
        string? instanceId,
        bool notificationHubSelected = false) =>
        SelectedKey = WorkspaceSidebarHelper.ResolveSelectionKey(section, instanceId, notificationHubSelected);

    public void ApplyNotificationHubBadge(int totalUnread) =>
        NotificationHubBadgeCount = WorkspaceSidebarHelper.ClampBadgeCount(totalUnread);
}
