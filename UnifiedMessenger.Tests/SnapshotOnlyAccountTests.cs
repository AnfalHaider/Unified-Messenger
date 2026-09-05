using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Accounts that produce chat-snapshot data but no <c>ThreadData</c> (Increment 126).
///
/// <para>
/// The rollup is driven by <c>ThreadRegistryService</c>, which only the WhatsApp ingress pipeline writes.
/// Instagram reads its own store and writes only the chat snapshot, so it produced no entity and no card —
/// while its waiting customers still reached the needs-a-reply queue, which reads the snapshot directly.
/// An account contributing to the queue and missing from the account list above it is worse than either
/// failure alone: the two figures cannot be reconciled by eye.
/// </para>
/// </summary>
public class SnapshotOnlyAccountTests
{
    private static MessengerInstance Instance(string id, string platform = "instagram", string name = "Insta DHA-2") =>
        new() { Id = id, DisplayName = name, Platform = platform };

    private static OversightCommandCenterSnapshot Build(
        IReadOnlyList<MessengerInstance> instances,
        Func<string, (int Active, int CaughtUp)?> chatSnapshot,
        OversightGrouping grouping = OversightGrouping.ByInstance,
        Func<string, string>? locationForInstance = null,
        Func<string, bool>? isSignedOut = null,
        IReadOnlyList<ThreadData>? threads = null) =>
        OversightRollupBuilder.Build(
            threads ?? [],
            instances,
            grouping,
            _ => 15,
            chatSnapshot: chatSnapshot,
            locationForInstance: locationForInstance,
            isSignedOut: isSignedOut,
            capabilitiesForInstance: id => PlatformDefinition.CapabilitiesFor(
                instances.FirstOrDefault(i => i.Id == id)?.Platform));

    [Fact]
    public void AnAccountWithSnapshotDataButNoThreadsStillGetsAnEntity()
    {
        var snapshot = Build([Instance("ig-1")], _ => (Active: 15, CaughtUp: 11));

        var entity = Assert.Single(snapshot.Entities);
        Assert.Equal("ig-1", entity.Key);
        Assert.Equal(15, entity.MeasuredCount);
        Assert.Equal(4, entity.AwaitingCount);
        Assert.True(entity.HasChatData);
        Assert.Equal("ig-1", Assert.Single(entity.MemberInstanceIds));
    }

    [Fact]
    public void AnAccountWithNoSnapshotAtAllGetsNoEntity()
    {
        // Nothing has been read. A zeroed card here would state that nobody is waiting on a channel the
        // app cannot see — the exact false calm the sign-in gate exists to prevent.
        var snapshot = Build([Instance("ig-1")], _ => null);

        Assert.Empty(snapshot.Entities);
    }

    [Fact]
    public void TheOnTimePercentComesFromTheSnapshotRatherThanBeingAssumed()
    {
        var snapshot = Build([Instance("ig-1")], _ => (Active: 10, CaughtUp: 5));

        Assert.Equal(50, Assert.Single(snapshot.Entities).OnTimePercent);
    }

    [Fact]
    public void NoHistoryIsRenderedAsAbsentRatherThanAsQuietDays()
    {
        var entity = Assert.Single(Build([Instance("ig-1")], _ => (Active: 3, CaughtUp: 0)).Entities);

        // A sparkline of seven zeroes reads as "seven quiet days"; an empty one renders nothing, which is
        // what "no history was read" should look like.
        Assert.Empty(entity.TrendCounts);
        Assert.Equal(0, entity.HistoricalOpenCount);
        Assert.Null(entity.LastActivityUtc);
    }

    [Fact]
    public void InstagramDoesNotClaimResponseTiming()
    {
        var entity = Assert.Single(Build([Instance("ig-1")], _ => (Active: 5, CaughtUp: 2)).Entities);

        // SupportsFrt is false for Instagram, so it is excluded from the on-time denominator elsewhere
        // rather than scored as a miss on timing it cannot supply.
        Assert.False(entity.SupportsResponseTiming);
    }

    [Fact]
    public void ASignedOutSnapshotOnlyAccountIsMarkedSignedOut()
    {
        var entity = Assert.Single(
            Build([Instance("ig-1")], _ => (Active: 2, CaughtUp: 0), isSignedOut: _ => true).Entities);

        Assert.True(entity.IsSignedOut);
    }

    [Fact]
    public void UnderLocationGroupingItJoinsTheExistingLocationRatherThanDuplicatingIt()
    {
        var wa = Instance("wa-1", "whatsapp", "WhatsApp DHA-2");
        var ig = Instance("ig-1");

        var threads = new List<ThreadData>
        {
            new() { InstanceId = "wa-1", ThreadId = "t1", Platform = "whatsapp", LastMessageTime = DateTimeOffset.UtcNow }
        };

        var snapshot = Build(
            [wa, ig],
            id => id == "ig-1" ? (Active: 15, CaughtUp: 11) : (Active: 20, CaughtUp: 18),
            OversightGrouping.ByLocation,
            locationForInstance: _ => "DHA-2",
            threads: threads);

        // One location, not two rows with the same name.
        var entity = Assert.Single(snapshot.Entities);
        Assert.Equal("DHA-2", entity.Key);
        Assert.Equal(2, entity.AccountCount);
        Assert.Contains("wa-1", entity.MemberInstanceIds);
        Assert.Contains("ig-1", entity.MemberInstanceIds);

        // 20 + 15 measured, 2 + 4 awaiting.
        Assert.Equal(35, entity.MeasuredCount);
        Assert.Equal(6, entity.AwaitingCount);
    }

    [Fact]
    public void ALocationMixingAChannelWithoutReplyTimesStopsClaimingTiming()
    {
        var wa = Instance("wa-1", "whatsapp", "WhatsApp DHA-2");
        var ig = Instance("ig-1");
        var threads = new List<ThreadData>
        {
            new() { InstanceId = "wa-1", ThreadId = "t1", Platform = "whatsapp", LastMessageTime = DateTimeOffset.UtcNow }
        };

        var entity = Assert.Single(Build(
            [wa, ig],
            _ => (Active: 4, CaughtUp: 4),
            OversightGrouping.ByLocation,
            locationForInstance: _ => "DHA-2",
            threads: threads).Entities);

        // WhatsApp supports FRT and Instagram does not, so the merged location cannot claim its reply
        // times are measured across everything it now contains.
        Assert.False(entity.SupportsResponseTiming);
    }

    [Fact]
    public void AnAccountAlreadyCoveredByThreadsIsNotAddedTwice()
    {
        var wa = Instance("wa-1", "whatsapp", "WhatsApp DHA-2");
        var threads = new List<ThreadData>
        {
            new() { InstanceId = "wa-1", ThreadId = "t1", Platform = "whatsapp", LastMessageTime = DateTimeOffset.UtcNow }
        };

        var snapshot = Build([wa], _ => (Active: 5, CaughtUp: 5), threads: threads);

        Assert.Single(snapshot.Entities);
        Assert.Single(snapshot.Entities[0].MemberInstanceIds);
    }
}
