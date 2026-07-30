using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// A channel we cannot measure is not a channel that failed. These pin the rule that reply-timing-incapable
/// channels are dropped from BOTH sides of the on-time fraction, so they can neither invent breaches nor
/// inflate the percentage.
/// </summary>
public class OversightRollupCapabilityTests
{
    private static MessengerInstance Inst(string id, string platform) =>
        new() { Id = id, DisplayName = id, ProfileName = id, Platform = platform };

    private static ThreadData T(
        string instanceId,
        bool replied,
        double latency,
        double replyLatency = 0,
        int urgency = 1) =>
        new()
        {
            ThreadId = Guid.NewGuid().ToString("N"),
            Platform = "whatsapp",
            InstanceId = instanceId,
            InstanceDisplayName = instanceId,
            BranchName = "B",
            IsReplied = replied,
            UrgencyScore = urgency,
            LatencyMinutes = latency,
            ReplyLatencyMinutes = replyLatency,
            LastMessageTime = DateTimeOffset.UtcNow
        };

    private static readonly PlatformCapabilities Timed = new()
    {
        IsMessageChannel = true,
        CanReadUnread = true,
        CanReadTimestamps = true,
        SupportsFrt = true
    };

    // Badge-only: an unread count is readable, per-conversation timing is not. This is the Meta shape.
    private static readonly PlatformCapabilities BadgeOnly = new()
    {
        IsMessageChannel = true,
        CanReadUnread = true,
        RequiresThreadOpenToRead = true
    };

    [Fact]
    public void TimingIncapableThreads_AreExcludedFromTheOnTimeDenominator()
    {
        // Two badge-only threads far past a 15-minute SLA. Without capability gating these would read as
        // 0% on-time and 2 breaches -- failures the channel never actually had, because we cannot even see
        // when the customer wrote.
        var threads = new List<ThreadData> { T("meta", replied: false, latency: 600), T("meta", replied: false, latency: 900) };
        var instances = new List<MessengerInstance> { Inst("meta", "messenger") };

        var snap = OversightRollupBuilder.Build(
            threads, instances, OversightGrouping.ByInstance, _ => 15,
            capabilitiesForInstance: _ => BadgeOnly);

        var entity = snap.Entities.Single();
        Assert.Equal(0, entity.MeasuredCount);
        Assert.Equal(0, entity.SlaBreachedCount);
        Assert.False(entity.SupportsResponseTiming);
        // Awaiting is unread-based, not timing-based, so it still counts both customers.
        Assert.Equal(2, entity.AwaitingCount);
    }

    [Fact]
    public void MixedLocation_ScoresOnlyTheMeasurableChannel()
    {
        // One location, two accounts: WhatsApp (measurable, one breach) and Messenger (badge-only, two
        // apparent breaches). The location's on-time% must reflect ONLY the WhatsApp thread.
        var threads = new List<ThreadData>
        {
            T("wa", replied: false, latency: 600),
            T("meta", replied: false, latency: 600),
            T("meta", replied: false, latency: 900)
        };
        var instances = new List<MessengerInstance> { Inst("wa", "whatsapp"), Inst("meta", "messenger") };

        var snap = OversightRollupBuilder.Build(
            threads, instances, OversightGrouping.ByLocation, _ => 15,
            capabilitiesForInstance: id => id == "wa" ? Timed : BadgeOnly,
            locationForInstance: _ => "Branch 1");

        var entity = snap.Entities.Single();
        Assert.Equal(1, entity.MeasuredCount);
        Assert.Equal(1, entity.SlaBreachedCount);
        Assert.Equal(0, entity.OnTimePercent);
        // At least one member channel can be timed, so the percentage carries information.
        Assert.True(entity.SupportsResponseTiming);
        Assert.Equal(3, entity.AwaitingCount);
    }

    [Fact]
    public void NoResolver_PreservesPreCapabilityBehaviour()
    {
        // The resolver is optional. Omitting it must reproduce the original numbers exactly, so existing
        // callers and tests are unaffected by the capability plumbing.
        var threads = new List<ThreadData> { T("a", replied: false, latency: 600), T("a", replied: true, latency: 5, replyLatency: 5) };
        var instances = new List<MessengerInstance> { Inst("a", "whatsapp") };

        var withoutResolver = OversightRollupBuilder.Build(threads, instances, OversightGrouping.ByInstance, _ => 15);
        var withFullResolver = OversightRollupBuilder.Build(
            threads, instances, OversightGrouping.ByInstance, _ => 15,
            capabilitiesForInstance: _ => Timed);

        var a = withoutResolver.Entities.Single();
        var b = withFullResolver.Entities.Single();
        Assert.Equal(b.MeasuredCount, a.MeasuredCount);
        Assert.Equal(b.OnTimePercent, a.OnTimePercent);
        Assert.Equal(b.SlaBreachedCount, a.SlaBreachedCount);
        Assert.True(a.SupportsResponseTiming);
        Assert.Equal(2, a.MeasuredCount);
        Assert.Equal(1, a.SlaBreachedCount);
    }

    [Fact]
    public void RepliedThreadOnTimeCheck_AlsoRespectsCapability()
    {
        // A badge-only channel's REPLIED threads must not pad the numerator either -- excluded from both
        // sides, not just the breach count.
        var threads = new List<ThreadData>
        {
            T("meta", replied: true, latency: 5, replyLatency: 1),
            T("meta", replied: true, latency: 5, replyLatency: 1)
        };
        var instances = new List<MessengerInstance> { Inst("meta", "messenger") };

        var snap = OversightRollupBuilder.Build(
            threads, instances, OversightGrouping.ByInstance, _ => 15,
            capabilitiesForInstance: _ => BadgeOnly);

        var entity = snap.Entities.Single();
        Assert.Equal(0, entity.MeasuredCount);
        Assert.False(entity.SupportsResponseTiming);
    }
}
