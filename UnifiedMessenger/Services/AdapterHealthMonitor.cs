using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class AdapterHealthMonitor
{
    private static readonly Lazy<AdapterHealthMonitor> LazyInstance = new(() => new AdapterHealthMonitor());

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(90);

    private readonly Dictionary<string, AdapterHealthStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _staleCheckTimer;

    public static AdapterHealthMonitor Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    private AdapterHealthMonitor()
    {
        _staleCheckTimer = new Timer(_ => CheckForStaleAdapters(), null, StaleThreshold, StaleThreshold);
    }

    public AdapterHealthStatus GetStatus(string instanceId)
    {
        return _statuses.TryGetValue(instanceId, out var status)
            ? status
            : new AdapterHealthStatus();
    }

    public void MarkNoAdapter(string instanceId)
    {
        SetStatus(instanceId, new AdapterHealthStatus
        {
            State = AdapterHealthState.NoAdapter
        });
    }

    public void MarkReady(string instanceId, string adapterId)
    {
        SetStatus(instanceId, new AdapterHealthStatus
        {
            State = AdapterHealthState.Ready,
            AdapterId = adapterId,
            LastHeartbeat = DateTimeOffset.UtcNow
        });
    }

    public void RecordHeartbeat(string instanceId, string? adapterId = null)
    {
        if (!_statuses.TryGetValue(instanceId, out var status))
        {
            status = new AdapterHealthStatus();
            _statuses[instanceId] = status;
        }

        status.State = AdapterHealthState.Healthy;
        status.LastHeartbeat = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(adapterId))
        {
            status.AdapterId = adapterId;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveInstance(string instanceId)
    {
        if (_statuses.Remove(instanceId))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetStatus(string instanceId, AdapterHealthStatus status)
    {
        _statuses[instanceId] = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CheckForStaleAdapters()
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;

        foreach (var status in _statuses.Values)
        {
            if (status.State is AdapterHealthState.NoAdapter or AdapterHealthState.Unknown)
            {
                continue;
            }

            if (status.LastHeartbeat is null ||
                now - status.LastHeartbeat.Value > StaleThreshold)
            {
                if (status.State != AdapterHealthState.Stale)
                {
                    status.State = AdapterHealthState.Stale;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
