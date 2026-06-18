using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class OversightRollupBuilderTests
{
    private static MessengerInstance Inst(string id, string name) =>
        new() { Id = id, DisplayName = name, ProfileName = id, Platform = "whatsapp" };

    private static ThreadData T(
        string instanceId,
        string branch,
        bool replied,
        int urgency,
        double latency,
        double replyLatency = 0,
        bool dropped = false,
        bool spam = false,
        bool backfilled = false,
        DateTimeOffset? lastMessage = null) =>
        new()
        {
            ThreadId = Guid.NewGuid().ToString("N"),
            Platform = "whatsapp",
            InstanceId = instanceId,
            InstanceDisplayName = instanceId,
            BranchName = branch,
            IsReplied = replied,
            UrgencyScore = urgency,
            LatencyMinutes = latency,
            ReplyLatencyMinutes = replyLatency,
            IsRevenueLeakageRisk = dropped,
            IsSpamOrPromo = spam,
            IsBackfilled = backfilled,
            LastMessageTime = lastMessage ?? DateTimeOffset.UtcNow
        };

    private static readonly List<MessengerInstance> Instances =
    [
        Inst("a", "A acct"), Inst("b", "B acct"), Inst("c", "C acct")
    ];

    private static List<ThreadData> Sample() =>
    [
        T("a", "F-11", replied: true, urgency: 1, latency: 5, replyLatency: 5),
        T("a", "F-11", replied: false, urgency: 5, latency: 5),
        T("b", "F-11", replied: false, urgency: 1, latency: 100, dropped: true),
        T("c", "DHA", replied: false, urgency: 5, latency: 100),
        T("c", "DHA", replied: false, urgency: 5, latency: 100),
        T("c", "DHA", replied: false, urgency: 1, latency: 1, spam: true)
    ];

    [Fact]
    public void ByInstance_CountsAndSortsWorstFirst()
    {
        var snap = OversightRollupBuilder.Build(Sample(), Instances, OversightGrouping.ByInstance, _ => 15);

        Assert.Equal(3, snap.Entities.Count);
        Assert.Equal(3, snap.TotalUrgent);
        Assert.Equal("c", snap.Entities[0].Key);
        Assert.Equal("c", snap.WorstEntityKey);
        Assert.Contains("need a reply now", snap.AttentionSummary);

        var a = snap.Entities.First(e => e.Key == "a");
        Assert.Equal(1, a.UrgentCount);
        Assert.Equal(100, a.OnTimePercent);
        Assert.Equal("A acct", a.DisplayName);

        var b = snap.Entities.First(e => e.Key == "b");
        Assert.Equal(0, b.UrgentCount);
        Assert.Equal(1, b.DroppedCount);
        Assert.Equal(0, b.OnTimePercent);
    }

    [Fact]
    public void ByLocation_AggregatesAccountsAndComputesOnTime()
    {
        var snap = OversightRollupBuilder.Build(Sample(), Instances, OversightGrouping.ByLocation, _ => 15);

        Assert.Equal(2, snap.Entities.Count);
        Assert.Equal("DHA", snap.Entities[0].Key);

        var f11 = snap.Entities.First(e => e.Key == "F-11");
        Assert.Equal(OversightEntityKind.Location, f11.Kind);
        Assert.Equal(2, f11.AccountCount);
        Assert.Equal(1, f11.UrgentCount);
        Assert.Equal(1, f11.DroppedCount);
        Assert.Equal(67, f11.OnTimePercent);
    }

    [Fact]
    public void AllReplied_SaysCaughtUp()
    {
        var threads = new List<ThreadData> { T("a", "F-11", replied: true, urgency: 1, latency: 5, replyLatency: 5) };
        var snap = OversightRollupBuilder.Build(threads, Instances, OversightGrouping.ByInstance, _ => 15);

        Assert.Equal(0, snap.TotalUrgent);
        Assert.Null(snap.WorstEntityKey);
        Assert.Equal("All caught up.", snap.AttentionSummary);
    }

    [Fact]
    public void ByLocation_PerInstanceResolver_DemotesGuidKeyToFriendlyName_AndKeepsAccountWhole()
    {
        // One account ("a") whose threads carry inconsistent branch values, including a raw GUID.
        var guid = Guid.NewGuid().ToString();
        var threads = new List<ThreadData>
        {
            T("a", "General", replied: false, urgency: 5, latency: 100),
            T("a", guid, replied: false, urgency: 1, latency: 100)
        };

        // Per-instance resolver returns the GUID (as a real instance.BranchKey would).
        var snap = OversightRollupBuilder.Build(
            threads, Instances, OversightGrouping.ByLocation, _ => 15,
            locationForInstance: _ => guid);

        // Account stays whole in ONE location, and the GUID never reaches the UI.
        Assert.Single(snap.Entities);
        var loc = snap.Entities[0];
        Assert.Equal("a", loc.DisplayName); // InstanceDisplayName fallback
        Assert.DoesNotContain(guid, loc.Key);
        Assert.Equal(2, loc.OpenCount);
    }

    [Fact]
    public void OnTime_WindowScopesToActiveConversations_OlderBecomesHistory()
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); // start of today (UTC)

        var threads = new List<ThreadData>
        {
            // Active today, open, inside SLA → the only measured thread, on time.
            T("a", "F-11", replied: false, urgency: 1, latency: 1, lastMessage: now),
            // Open but last active 3 days ago → carried backlog, excluded from today's on-time.
            T("a", "F-11", replied: false, urgency: 1, latency: 9000, lastMessage: now.AddDays(-3))
        };

        var a = OversightRollupBuilder
            .Build(threads, Instances, OversightGrouping.ByInstance, _ => 15, windowStartUtc: windowStart)
            .Entities.Single();

        Assert.Equal(1, a.MeasuredCount);          // only today's conversation
        Assert.Equal(100, a.OnTimePercent);        // it's within SLA
        Assert.Equal(1, a.HistoricalOpenCount);    // the 3-day-old open is "from history"
    }

    [Fact]
    public void OnTime_WindowIncludesPreConnectMessagesFromToday()
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        // A backfilled conversation that arrived earlier today (before the account was connected) is
        // still "today" — it counts toward the window, not just live post-connect traffic.
        var threads = new List<ThreadData>
        {
            T("a", "F-11", replied: true, urgency: 1, latency: 5, replyLatency: 5, backfilled: true, lastMessage: now),
            T("a", "F-11", replied: false, urgency: 1, latency: 1, lastMessage: now)
        };

        var a = OversightRollupBuilder
            .Build(threads, Instances, OversightGrouping.ByInstance, _ => 15, windowStartUtc: windowStart)
            .Entities.Single();

        Assert.Equal(2, a.MeasuredCount);
        Assert.Equal(100, a.OnTimePercent);
        Assert.Equal(0, a.HistoricalOpenCount);
    }

    [Fact]
    public void ChatSnapshot_OverridesOnTimeWithCaughtUpPercent()
    {
        // Thread-based view would read 0% (an old unreplied breach), but the unread snapshot says the
        // account is 95% caught up — the snapshot wins.
        var threads = new List<ThreadData>
        {
            T("a", "F-11", replied: false, urgency: 1, latency: 9000)
        };

        var entity = OversightRollupBuilder
            .Build(threads, Instances, OversightGrouping.ByInstance, _ => 15,
                chatSnapshot: id => id == "a" ? (100, 95) : null)
            .Entities.Single();

        Assert.Equal(95, entity.OnTimePercent);
        Assert.Equal(100, entity.MeasuredCount);
        Assert.Equal(5, entity.AwaitingCount); // 100 active − 95 caught up
    }

    [Fact]
    public void Stale_WhenAllInstanceConnectionsStale()
    {
        var snap = OversightRollupBuilder.Build(
            Sample(), Instances, OversightGrouping.ByInstance, _ => 15, isStale: _ => true);

        Assert.All(snap.Entities, e => Assert.True(e.IsStale));
    }
}
