using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public enum NotificationChangeKind
{
    BadgeUpdated,
    AlertAdded,
    AlertRemoved,
    AlertUpdated,
    AlertsCleared,
    AllAlertsMarkedRead
}

public sealed class NotificationHubChangedEventArgs : EventArgs
{
    public NotificationChangeKind Kind { get; init; }

    public NotificationAlert? Alert { get; init; }
}

public sealed class NotificationHub
{
    private static readonly Lazy<NotificationHub> LazyInstance = new(() => new NotificationHub());

    private readonly Dictionary<string, int> _badgeCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NotificationAlert> _alerts = [];
    private readonly HashSet<string> _mutedInstances = new(StringComparer.OrdinalIgnoreCase);

    public static NotificationHub Instance => LazyInstance.Value;

    public event EventHandler<NotificationHubChangedEventArgs>? Changed;

    public IReadOnlyList<NotificationAlert> Alerts => _alerts;

    public int TotalUnreadCount =>
        _badgeCounts
            .Where(pair => !_mutedInstances.Contains(pair.Key))
            .Sum(pair => pair.Value);

    public int UnreadAlertCount => _alerts.Count(a => !a.IsRead);

    public IReadOnlyList<NotificationAlert> GetAlertsSortedByInstance() =>
        _alerts
            .OrderBy(a => a.InstanceDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(a => a.ReceivedAt)
            .ToList();

    public void SyncMutedInstances(IEnumerable<MessengerInstance> instances)
    {
        _mutedInstances.Clear();
        foreach (var instance in instances.Where(i => i.NotificationsMuted))
        {
            _mutedInstances.Add(instance.Id);
        }
    }

    public bool IsInstanceMuted(string instanceId) =>
        _mutedInstances.Contains(instanceId);

    public IReadOnlyList<NotificationAlertGroup> GetAlertsGroupedByInstance() =>
        _alerts
            .GroupBy(a => a.InstanceDisplayName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new NotificationAlertGroup(
                g.Key,
                g.OrderByDescending(a => a.ReceivedAt)))
            .ToList();

    public int GetBadgeCount(string instanceId)
    {
        return _badgeCounts.TryGetValue(instanceId, out var count) ? count : 0;
    }

    public void UpdateBadgeCount(string instanceId, int count)
    {
        if (IsInstanceMuted(instanceId))
        {
            return;
        }

        count = Math.Max(0, count);
        _badgeCounts[instanceId] = count;
        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = NotificationChangeKind.BadgeUpdated
        });
    }

    public void AddAlert(NotificationAlert alert)
    {
        if (IsInstanceMuted(alert.InstanceId))
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-15);
        if (_alerts.Any(a =>
                a.InstanceId.Equals(alert.InstanceId, StringComparison.OrdinalIgnoreCase) &&
                a.Title.Equals(alert.Title, StringComparison.Ordinal) &&
                a.Body.Equals(alert.Body, StringComparison.Ordinal) &&
                a.ReceivedAt >= cutoff))
        {
            return;
        }

        _alerts.Insert(0, alert);

        if (_alerts.Count > 200)
        {
            _alerts.RemoveRange(200, _alerts.Count - 200);
        }

        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = NotificationChangeKind.AlertAdded,
            Alert = alert
        });
    }

    public void DismissAlert(string alertId)
    {
        var removed = _alerts.FirstOrDefault(a => a.Id.Equals(alertId, StringComparison.OrdinalIgnoreCase));
        if (removed is null)
        {
            return;
        }

        _alerts.Remove(removed);
        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = NotificationChangeKind.AlertRemoved,
            Alert = removed
        });
    }

    public void MarkAlertRead(string alertId)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id.Equals(alertId, StringComparison.OrdinalIgnoreCase));
        if (alert is null || alert.IsRead)
        {
            return;
        }

        alert.IsRead = true;
        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = NotificationChangeKind.AlertUpdated,
            Alert = alert
        });
    }

    public void MarkAllAlertsRead()
    {
        var changed = false;
        foreach (var alert in _alerts.Where(a => !a.IsRead))
        {
            alert.IsRead = true;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = NotificationChangeKind.AllAlertsMarkedRead
        });
    }

    public void RemoveAlertsForInstance(string instanceId)
    {
        _alerts.RemoveAll(a => a.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        _badgeCounts.Remove(instanceId);
        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = NotificationChangeKind.AlertsCleared
        });
    }

    public void ClearAlerts()
    {
        if (_alerts.Count == 0)
        {
            return;
        }

        _alerts.Clear();
        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = NotificationChangeKind.AlertsCleared
        });
    }
}
