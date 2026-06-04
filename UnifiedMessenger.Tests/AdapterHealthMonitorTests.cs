using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class AdapterHealthMonitorTests
{
    [Fact]
    public void EvaluateIsStale_ReturnsFalseForNoAdapterState()
    {
        var status = new AdapterHealthStatus { State = AdapterHealthState.NoAdapter };

        Assert.False(AdapterHealthMonitor.EvaluateIsStale(
            status,
            DateTimeOffset.UtcNow,
            AdapterHealthMonitor.StaleThreshold));
    }

    [Fact]
    public void EvaluateIsStale_ReturnsTrueWhenHeartbeatMissing()
    {
        var status = new AdapterHealthStatus
        {
            State = AdapterHealthState.Ready,
            LastHeartbeat = null
        };

        Assert.True(AdapterHealthMonitor.EvaluateIsStale(
            status,
            DateTimeOffset.UtcNow,
            AdapterHealthMonitor.StaleThreshold));
    }

    [Fact]
    public void EvaluateIsStale_ReturnsFalseForRecentHeartbeat()
    {
        var status = new AdapterHealthStatus
        {
            State = AdapterHealthState.Healthy,
            LastHeartbeat = DateTimeOffset.UtcNow.AddSeconds(-10)
        };

        Assert.False(AdapterHealthMonitor.EvaluateIsStale(
            status,
            DateTimeOffset.UtcNow,
            AdapterHealthMonitor.StaleThreshold));
    }

    [Fact]
    public void GetStatus_ReturnsClone()
    {
        var monitor = AdapterHealthMonitor.Instance;
        monitor.MarkReady("inst-clone", "whatsapp");

        var snapshot = monitor.GetStatus("inst-clone");
        snapshot.State = AdapterHealthState.Stale;

        Assert.Equal(AdapterHealthState.Ready, monitor.GetStatus("inst-clone").State);

        monitor.RemoveInstance("inst-clone");
    }

    [Fact]
    public void RecordHeartbeat_DoesNotRaiseChangedForRepeatedHealthyHeartbeats()
    {
        var monitor = AdapterHealthMonitor.Instance;
        monitor.RemoveInstance("inst-heartbeat");

        var changeCount = 0;
        void OnChanged(object? sender, EventArgs e) => changeCount += 1;
        monitor.Changed += OnChanged;

        try
        {
            monitor.MarkReady("inst-heartbeat", "slack");
            monitor.RecordHeartbeat("inst-heartbeat", "slack");
            var changesAfterFirstHeartbeat = changeCount;

            monitor.RecordHeartbeat("inst-heartbeat", "slack");

            Assert.Equal(changesAfterFirstHeartbeat, changeCount);
            Assert.Equal(AdapterHealthState.Healthy, monitor.GetStatus("inst-heartbeat").State);
        }
        finally
        {
            monitor.Changed -= OnChanged;
            monitor.RemoveInstance("inst-heartbeat");
        }
    }

    [Fact]
    public void CheckForStaleAdapters_TransitionsReadyToStale()
    {
        var monitor = AdapterHealthMonitor.Instance;
        monitor.RemoveInstance("inst-stale");

        monitor.MarkReady("inst-stale", "telegram");
        monitor.CheckForStaleAdapters(DateTimeOffset.UtcNow.AddMinutes(2));

        Assert.Equal(AdapterHealthState.Stale, monitor.GetStatus("inst-stale").State);

        monitor.RemoveInstance("inst-stale");
    }

    [Fact]
    public void TryBeginRecovery_PreventsConcurrentRecovery()
    {
        var monitor = AdapterHealthMonitor.Instance;

        Assert.True(monitor.TryBeginRecovery("inst-recover"));
        Assert.False(monitor.TryBeginRecovery("inst-recover"));

        monitor.EndRecovery("inst-recover");
        Assert.True(monitor.TryBeginRecovery("inst-recover"));

        monitor.EndRecovery("inst-recover");
    }
}
