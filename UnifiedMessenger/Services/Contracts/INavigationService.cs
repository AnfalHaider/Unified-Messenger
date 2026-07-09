using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface INavigationService
{
    event EventHandler<InstanceNavigationRequest>? InstanceNavigationRequested;

    event EventHandler? DashboardRefreshRequested;

    event EventHandler<string>? ArchivedInstanceRestoreRequested;

    event EventHandler? LayoutRefreshRequested;

    event EventHandler? InstanceRegistryRefreshRequested;

    event EventHandler? AddInstanceRequested;

    event EventHandler<string?>? OccBranchFilterRequested;

    event EventHandler? OccImmediateLaneFocusRequested;

    event EventHandler? OccUrgentQueueFilterRequested;

    event EventHandler<string?>? SettingsOpenRequested;

    event EventHandler<InstanceNavigationFailedEventArgs>? InstanceNavigationFailed;

    void RequestInstance(string instanceId);

    void RequestInstance(string instanceId, string? conversationKey, string? customerName = null);

    void RequestDashboardRefresh();

    void RequestArchivedInstanceRestore(string instanceId);

    void RequestLayoutRefresh();

    void RequestInstanceRegistryRefresh();

    void RequestAddInstance();

    void RequestOccBranchFilter(string? branchKey);

    void RequestOccImmediateLaneFocus();

    void RequestOccUrgentQueueFilter();

    void RequestOpenSettings(string? sectionKey = null);

    void OpenInstance(string instanceId);

    void OpenInstance(string instanceId, string? conversationKey, string? customerName = null);

    void NotifyNavigationFailed(InstanceNavigationFailedEventArgs args);
}
