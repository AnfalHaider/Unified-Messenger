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
    internal const int MaxAlerts = 200;

    internal static TimeSpan DuplicateAlertWindow { get; } = TimeSpan.FromSeconds(15);

    private static readonly Lazy<NotificationHub> LazyInstance = new(() => new NotificationHub());

    private readonly Dictionary<string, int> _badgeCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NotificationAlert> _alerts = [];
    private readonly HashSet<string> _mutedInstances = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public static NotificationHub Instance => LazyInstance.Value;

    public event EventHandler<NotificationHubChangedEventArgs>? Changed;

    public IReadOnlyList<NotificationAlert> Alerts
    {
        get
        {
            lock (_gate)
            {
                return _alerts.Where(IsVisibleAlert).ToList();
            }
        }
    }

    public int TotalUnreadCount
    {
        get
        {
            lock (_gate)
            {
                return _badgeCounts
                    .Where(pair => !IsInstanceMuted(pair.Key))
                    .Sum(pair => pair.Value);
            }
        }
    }

    public int UnreadAlertCount
    {
        get
        {
            lock (_gate)
            {
                return _alerts.Count(a => !a.IsRead && IsVisibleAlert(a));
            }
        }
    }

    private NotificationHub()
    {
    }

    internal static NotificationHub CreateForTests() => new();

    public IReadOnlyList<NotificationAlert> GetAlertsSortedByInstance()
    {
        lock (_gate)
        {
            return _alerts
                .Where(IsVisibleAlert)
                .OrderBy(a => a.InstanceDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(a => a.ReceivedAt)
                .ToList();
        }
    }

    public void SyncMutedInstances(IEnumerable<MessengerInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var nextMuted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in instances.Where(i => i.NotificationsMuted))
        {
            if (!string.IsNullOrWhiteSpace(instance.Id))
            {
                nextMuted.Add(instance.Id);
            }
        }

        lock (_gate)
        {
            var changed = nextMuted.Count != _mutedInstances.Count ||
                          nextMuted.Any(id => !_mutedInstances.Contains(id));

            _mutedInstances.Clear();
            foreach (var id in nextMuted)
            {
                _mutedInstances.Add(id);
            }

            if (changed)
            {
                RaiseChanged(NotificationChangeKind.BadgeUpdated);
            }
        }
    }

    public bool IsInstanceMuted(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        lock (_gate)
        {
            return _mutedInstances.Contains(instanceId);
        }
    }

    public IReadOnlyList<NotificationAlertGroup> GetAlertsGroupedByInstance()
    {
        lock (_gate)
        {
            return _alerts
                .Where(IsVisibleAlert)
                .GroupBy(a => a.InstanceId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.First().InstanceDisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new NotificationAlertGroup(
                    g.Key,
                    g.First().InstanceDisplayName,
                    g.OrderByDescending(a => a.ReceivedAt)))
                .ToList();
        }
    }

    public int GetBadgeCount(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return 0;
        }

        lock (_gate)
        {
            if (IsInstanceMuted(instanceId))
            {
                return 0;
            }

            return _badgeCounts.TryGetValue(instanceId, out var count) ? count : 0;
        }
    }

    public void UpdateBadgeCount(string instanceId, int count)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        count = Math.Max(0, count);

        lock (_gate)
        {
            if (IsInstanceMuted(instanceId) && count > 0)
            {
                return;
            }

            if (_badgeCounts.TryGetValue(instanceId, out var existing) && existing == count)
            {
                return;
            }

            _badgeCounts[instanceId] = count;
            RaiseChanged(NotificationChangeKind.BadgeUpdated);
        }
    }

    public void AddAlert(NotificationAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (string.IsNullOrWhiteSpace(alert.InstanceId) || IsInstanceMuted(alert.InstanceId))
        {
            return;
        }

        lock (_gate)
        {
            if (IsInstanceMuted(alert.InstanceId))
            {
                return;
            }

            var cutoff = DateTimeOffset.UtcNow - DuplicateAlertWindow;
            if (_alerts.Any(a =>
                    a.InstanceId.Equals(alert.InstanceId, StringComparison.OrdinalIgnoreCase) &&
                    a.Title.Equals(alert.Title, StringComparison.Ordinal) &&
                    a.Body.Equals(alert.Body, StringComparison.Ordinal) &&
                    a.ReceivedAt >= cutoff))
            {
                return;
            }

            _alerts.Insert(0, alert);
            TrimAlertsLocked();

            RaiseChanged(NotificationChangeKind.AlertAdded, alert);
        }
    }

    public void DismissAlert(string alertId)
    {
        if (string.IsNullOrWhiteSpace(alertId))
        {
            return;
        }

        lock (_gate)
        {
            var removed = _alerts.FirstOrDefault(a => a.Id.Equals(alertId, StringComparison.OrdinalIgnoreCase));
            if (removed is null)
            {
                return;
            }

            _alerts.Remove(removed);
            RaiseChanged(NotificationChangeKind.AlertRemoved, removed);
        }
    }

    public void MarkAlertRead(string alertId)
    {
        if (string.IsNullOrWhiteSpace(alertId))
        {
            return;
        }

        lock (_gate)
        {
            var alert = _alerts.FirstOrDefault(a => a.Id.Equals(alertId, StringComparison.OrdinalIgnoreCase));
            if (alert is null || alert.IsRead)
            {
                return;
            }

            alert.IsRead = true;
            RaiseChanged(NotificationChangeKind.AlertUpdated, alert);
        }
    }

    public void MarkAllAlertsRead()
    {
        lock (_gate)
        {
            var changed = false;
            foreach (var alert in _alerts.Where(a => !a.IsRead && IsVisibleAlert(a)))
            {
                alert.IsRead = true;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            RaiseChanged(NotificationChangeKind.AllAlertsMarkedRead);
        }
    }

    public void RemoveAlertsForInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            var removedCount = _alerts.RemoveAll(a =>
                a.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));

            _badgeCounts.Remove(instanceId);
            _mutedInstances.Remove(instanceId);

            if (removedCount == 0 && !_badgeCounts.ContainsKey(instanceId))
            {
                return;
            }

            RaiseChanged(NotificationChangeKind.AlertsCleared);
        }
    }

    public void ClearAlerts()
    {
        lock (_gate)
        {
            if (_alerts.Count == 0)
            {
                return;
            }

            _alerts.Clear();
            RaiseChanged(NotificationChangeKind.AlertsCleared);
        }
    }

    internal bool IsVisibleAlert(NotificationAlert alert) =>
        !IsInstanceMuted(alert.InstanceId);

    private void TrimAlertsLocked()
    {
        if (_alerts.Count <= MaxAlerts)
        {
            return;
        }

        _alerts.RemoveRange(MaxAlerts, _alerts.Count - MaxAlerts);
    }

    private void RaiseChanged(NotificationChangeKind kind, NotificationAlert? alert = null) =>
        Changed?.Invoke(this, new NotificationHubChangedEventArgs
        {
            Kind = kind,
            Alert = alert
        });
}
