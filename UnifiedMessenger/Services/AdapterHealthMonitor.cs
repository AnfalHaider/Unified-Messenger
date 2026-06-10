using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class AdapterHealthMonitor
{
    private static readonly Lazy<AdapterHealthMonitor> LazyInstance = new(() => new AdapterHealthMonitor());

    internal static TimeSpan StaleThreshold { get; } = TimeSpan.FromSeconds(90);

    internal static TimeSpan StaleRecoveryCooldown { get; } = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, AdapterHealthStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastStaleNotification = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recoveryInProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly Timer _staleCheckTimer;

    public static AdapterHealthMonitor Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public event EventHandler<string>? AdapterStaleDetected;

    private AdapterHealthMonitor()
    {
        _staleCheckTimer = new Timer(_ => CheckForStaleAdapters(), null, StaleThreshold, StaleThreshold);
    }

    public AdapterHealthStatus GetStatus(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return new AdapterHealthStatus();
        }

        lock (_gate)
        {
            return _statuses.TryGetValue(instanceId, out var status)
                ? status.Clone()
                : new AdapterHealthStatus();
        }
    }

    public void MarkNoAdapter(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        SetStatus(instanceId, new AdapterHealthStatus
        {
            State = AdapterHealthState.NoAdapter
        });
    }

    public void MarkReady(string instanceId, string adapterId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        SetStatus(instanceId, new AdapterHealthStatus
        {
            State = AdapterHealthState.Ready,
            AdapterId = adapterId,
            LastHeartbeat = DateTimeOffset.UtcNow
        });
    }

    public void RecordHeartbeat(string instanceId, string? adapterId = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            if (!_statuses.TryGetValue(instanceId, out var status))
            {
                status = new AdapterHealthStatus();
                _statuses[instanceId] = status;
            }

            var previousState = status.State;
            status.State = AdapterHealthState.Healthy;
            status.LastHeartbeat = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(adapterId))
            {
                status.AdapterId = adapterId;
            }

            status.Normalize();
            _lastStaleNotification.Remove(instanceId);

            if (previousState != status.State)
            {
                RaiseChanged();
            }
        }
    }

    public void RemoveInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            var removed = _statuses.Remove(instanceId);
            _lastStaleNotification.Remove(instanceId);
            _recoveryInProgress.Remove(instanceId);

            if (removed)
            {
                RaiseChanged();
            }
        }
    }

    public bool TryBeginRecovery(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        lock (_gate)
        {
            return _recoveryInProgress.Add(instanceId);
        }
    }

    public void EndRecovery(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            _recoveryInProgress.Remove(instanceId);
        }
    }

    internal static bool EvaluateIsStale(AdapterHealthStatus status, DateTimeOffset now, TimeSpan threshold)
    {
        if (status.State is AdapterHealthState.NoAdapter or AdapterHealthState.Unknown)
        {
            return false;
        }

        return status.LastHeartbeat is null ||
               now - status.LastHeartbeat.Value > threshold;
    }

    internal void CheckForStaleAdapters(DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var changed = false;

        lock (_gate)
        {
            foreach (var (instanceId, status) in _statuses.ToList())
            {
                if (!string.IsNullOrWhiteSpace(status.AdapterId) &&
                    !PlatformModules.PlatformModuleRegistry.Instance.IsEnabled(status.AdapterId))
                {
                    continue;
                }

                if (!EvaluateIsStale(status, now, StaleThreshold))
                {
                    continue;
                }

                if (status.State != AdapterHealthState.Stale)
                {
                    status.State = AdapterHealthState.Stale;
                    status.Normalize();
                    changed = true;
                    NotifyStaleLocked(instanceId, now);
                    continue;
                }

                if (ShouldRetryRecoveryLocked(instanceId, now))
                {
                    NotifyStaleLocked(instanceId, now);
                }
            }

            if (changed)
            {
                RaiseChanged();
            }
        }
    }

    private void SetStatus(string instanceId, AdapterHealthStatus status)
    {
        status.Normalize();

        lock (_gate)
        {
            _statuses[instanceId] = status;
            _lastStaleNotification.Remove(instanceId);
            RaiseChanged();
        }
    }

    private bool ShouldRetryRecoveryLocked(string instanceId, DateTimeOffset now)
    {
        if (!_lastStaleNotification.TryGetValue(instanceId, out var lastNotified))
        {
            return true;
        }

        return now - lastNotified >= StaleRecoveryCooldown;
    }

    private void NotifyStaleLocked(string instanceId, DateTimeOffset now)
    {
        _lastStaleNotification[instanceId] = now;
        AdapterStaleDetected?.Invoke(this, instanceId);
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
