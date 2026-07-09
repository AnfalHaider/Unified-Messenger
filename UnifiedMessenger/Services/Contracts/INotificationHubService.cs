using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface INotificationHubService
{
    event EventHandler<NotificationHubChangedEventArgs>? Changed;

    IReadOnlyList<NotificationAlert> Alerts { get; }

    int TotalUnreadCount { get; }

    int UnreadAlertCount { get; }

    IReadOnlyList<NotificationAlert> GetAlertsSortedByInstance();

    IReadOnlyList<NotificationAlertGroup> GetAlertsGroupedByInstance();

    bool IsInstanceMuted(string instanceId);

    int GetBadgeCount(string instanceId);

    void UpdateBadgeCount(string instanceId, int count);

    void AddAlert(NotificationAlert alert);

    void DismissAlert(string alertId);

    void MarkAlertRead(string alertId);

    void MarkAllAlertsRead();

    void RemoveAlertsForInstance(string instanceId);

    void ClearAlerts();

    void SyncMutedInstances(IEnumerable<MessengerInstance> instances);
}
