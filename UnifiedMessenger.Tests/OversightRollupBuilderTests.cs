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
        bool spam = false) =>
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
            IsSpamOrPromo = spam
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
    public void Stale_WhenAllInstanceConnectionsStale()
    {
        var snap = OversightRollupBuilder.Build(
            Sample(), Instances, OversightGrouping.ByInstance, _ => 15, isStale: _ => true);

        Assert.All(snap.Entities, e => Assert.True(e.IsStale));
    }
}
