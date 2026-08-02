using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ChannelEventBusTests
{
    private static ChannelSnapshotEvent Snapshot(string instanceId = "acct-1", string source = "store-bridge") =>
        new(instanceId, "whatsapp", DateTimeOffset.UtcNow, Active: 10, CaughtUp: 8, Awaiting: 2, source);

    [Fact]
    public void Publish_ReachesEverySubscriber()
    {
        var received = new List<IChannelEvent>();
        using var a = ChannelEventBus.Instance.Subscribe(received.Add);
        using var b = ChannelEventBus.Instance.Subscribe(received.Add);

        ChannelEventBus.Instance.Publish(Snapshot());

        Assert.Equal(2, received.Count);
    }

    [Fact]
    public void Subscribe_TypedOverloadFiltersByEventType()
    {
        var snapshots = new List<ChannelSnapshotEvent>();
        using var subscription = ChannelEventBus.Instance.Subscribe<ChannelSnapshotEvent>(snapshots.Add);

        ChannelEventBus.Instance.Publish(Snapshot());
        ChannelEventBus.Instance.Publish(new ChannelSessionStateEvent(
            "acct-1", "whatsapp", DateTimeOffset.UtcNow, SessionState.Working, SessionState.Starting));

        Assert.Single(snapshots);
        Assert.Equal("store-bridge", snapshots[0].Source);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var count = 0;
        var subscription = ChannelEventBus.Instance.Subscribe(_ => count++);

        ChannelEventBus.Instance.Publish(Snapshot());
        subscription.Dispose();
        ChannelEventBus.Instance.Publish(Snapshot());

        Assert.Equal(1, count);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var subscription = ChannelEventBus.Instance.Subscribe(_ => { });
        var before = ChannelEventBus.Instance.SubscriberCount;

        subscription.Dispose();
        subscription.Dispose();

        Assert.Equal(before - 1, ChannelEventBus.Instance.SubscriberCount);
    }

    [Fact]
    public void Publish_AThrowingSubscriberDoesNotBreakThePublisherOrOtherSubscribers()
    {
        // A scraper must never fail because a dashboard handler threw — that would trade a cosmetic bug
        // for lost oversight data.
        var reached = false;
        using var bad = ChannelEventBus.Instance.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var good = ChannelEventBus.Instance.Subscribe(_ => reached = true);

        var exception = Record.Exception(() => ChannelEventBus.Instance.Publish(Snapshot()));

        Assert.Null(exception);
        Assert.True(reached);
    }

    [Fact]
    public void Publish_IgnoresNull()
    {
        var count = 0;
        using var subscription = ChannelEventBus.Instance.Subscribe(_ => count++);

        ChannelEventBus.Instance.Publish(null!);

        Assert.Equal(0, count);
    }
}

public class OversightAlertMonitorCadenceTests
{
    [Fact]
    public void ResolvePollInterval_UsesTheFastCadenceOnlyWhenEveryAccountIsOnTheBridge()
    {
        Assert.Equal(
            OversightAlertMonitor.BridgePollInterval,
            OversightAlertMonitor.ResolvePollInterval(bridgeActive: 3, attempted: 3));
    }

    [Fact]
    public void ResolvePollInterval_AMixedFleetStaysOnTheSlowCadence()
    {
        // One account falling back to the expensive IndexedDB reader must not get polled every 25s.
        Assert.Equal(
            OversightAlertMonitor.LegacyPollInterval,
            OversightAlertMonitor.ResolvePollInterval(bridgeActive: 2, attempted: 3));
    }

    [Fact]
    public void ResolvePollInterval_DefaultsToSlowBeforeAnythingHasBeenProbed()
    {
        Assert.Equal(
            OversightAlertMonitor.LegacyPollInterval,
            OversightAlertMonitor.ResolvePollInterval(bridgeActive: 0, attempted: 0));
    }
}
