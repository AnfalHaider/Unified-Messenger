using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class SessionStateProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(InstanceConnectionStatus.Error, SessionState.Failed)]
    [InlineData(InstanceConnectionStatus.LoggedOut, SessionState.ScanQr)]
    [InlineData(InstanceConnectionStatus.Initializing, SessionState.Starting)]
    public void Resolve_MapsNonConnectedStatusesDirectly(
        InstanceConnectionStatus status,
        SessionState expected)
    {
        Assert.Equal(expected, SessionStateProjection.Resolve(status, snapshotCapturedUtc: Now, Now));
    }

    [Fact]
    public void Resolve_ConnectedWithFreshSnapshotIsWorking()
    {
        var captured = Now - TimeSpan.FromMinutes(1);

        Assert.Equal(
            SessionState.Working,
            SessionStateProjection.Resolve(InstanceConnectionStatus.Connected, captured, Now));
    }

    [Fact]
    public void Resolve_ConnectedWithStaleSnapshotIsDegraded()
    {
        // The case this projection exists for: the page still says "connected" while the numbers rot.
        var captured = Now - SessionStateProjection.StaleSnapshotThreshold - TimeSpan.FromSeconds(1);

        Assert.Equal(
            SessionState.Degraded,
            SessionStateProjection.Resolve(InstanceConnectionStatus.Connected, captured, Now));
    }

    [Fact]
    public void Resolve_ConnectedButNeverReadIsStartingNotDegraded()
    {
        // No snapshot yet means "still coming up", not "broken" — showing Degraded here would cry wolf
        // on every launch.
        Assert.Equal(
            SessionState.Starting,
            SessionStateProjection.Resolve(InstanceConnectionStatus.Connected, snapshotCapturedUtc: null, Now));
    }

    [Fact]
    public void Resolve_SnapshotExactlyAtThresholdIsStillWorking()
    {
        var captured = Now - SessionStateProjection.StaleSnapshotThreshold;

        Assert.Equal(
            SessionState.Working,
            SessionStateProjection.Resolve(InstanceConnectionStatus.Connected, captured, Now));
    }

    [Fact]
    public void EveryState_HasALabelAndAPlainLanguageDescription()
    {
        foreach (var state in Enum.GetValues<SessionState>())
        {
            var label = SessionStateProjection.ToLabel(state);
            var description = SessionStateProjection.ToDescription(state);

            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.NotEqual("Unknown", label);
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.EndsWith(".", description, StringComparison.Ordinal);
        }
    }
}
