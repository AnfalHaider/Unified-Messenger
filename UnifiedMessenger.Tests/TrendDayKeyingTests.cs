using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-03 — the command-centre sparkline bucketed activity by UTC day while every other daily
/// figure in the product buckets by LOCAL day.
///
/// <para>
/// Analytics keys its daily buckets from <c>receivedAtUtc.LocalDateTime</c>
/// (<c>MessageAnalyticsService.cs:590,596</c>) and prunes with <c>DateTime.Now.Date</c> (<c>:560</c>);
/// <c>KpiTrendStore</c> keys with <c>LocalDateTime</c> (<c>:1478</c>). <c>OversightRollupBuilder.BuildTrend</c>
/// used <c>UtcDateTime.Date</c> on both sides of its subtraction.
/// </para>
/// <para>
/// For an owner at UTC+5, every message between local midnight and 05:00 fell into the *previous* UTC day,
/// so the card's 7-day sparkline disagreed with the Analytics daily chart for the same account and period.
/// </para>
/// <para>
/// <b>Test sensitivity caveat:</b> these tests only discriminate in a non-UTC time zone. On a machine set
/// to UTC (typical CI) local and UTC days coincide and they would pass against the old code too. They are
/// written against <see cref="TimeZoneInfo.Local"/> so they are meaningful on a developer machine in a
/// real zone — this one runs at UTC+5 — and harmless elsewhere.
/// </para>
/// </summary>
public class TrendDayKeyingTests
{
    private static MessengerInstance Inst(string id) =>
        new() { Id = id, DisplayName = id, ProfileName = id, Platform = "whatsapp" };

    private static ThreadData ThreadAt(DateTimeOffset lastMessage) =>
        new()
        {
            ThreadId = Guid.NewGuid().ToString("N"),
            Platform = "whatsapp",
            InstanceId = "acct",
            InstanceDisplayName = "acct",
            BranchName = "branch",
            IsReplied = false,
            UrgencyScore = 1,
            LatencyMinutes = 5,
            LastMessageTime = lastMessage
        };

    /// <summary>A local wall-clock time today, as a correctly-offset DateTimeOffset.</summary>
    private static DateTimeOffset LocalToday(int hour, int minute)
    {
        var localDate = DateTime.Now.Date.AddHours(hour).AddMinutes(minute);
        return new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
    }

    private static IReadOnlyList<int> TrendFor(DateTimeOffset lastMessage)
    {
        var snap = OversightRollupBuilder.Build(
            [ThreadAt(lastMessage)],
            [Inst("acct")],
            OversightGrouping.ByInstance,
            _ => 15);

        return snap.Entities.Single().TrendCounts;
    }

    [Fact]
    public void AMessageJustAfterLocalMidnightCountsAsToday()
    {
        // The failing case at UTC+5: 00:30 local is 19:30 the PREVIOUS day in UTC, so UTC keying filed it
        // under yesterday and today's bar read zero until 05:00 local.
        var trend = TrendFor(LocalToday(0, 30));

        Assert.Equal(1, trend[^1]);
    }

    [Fact]
    public void AMessageLateThisMorningAlsoCountsAsToday()
    {
        // Control: this one lands on the same day under either keying, so it isolates the boundary case.
        var trend = TrendFor(LocalToday(11, 0));

        Assert.Equal(1, trend[^1]);
    }

    [Fact]
    public void AMessageJustBeforeLocalMidnightCountsAsYesterday()
    {
        // The mirror boundary — 23:30 local yesterday must not leak forward into today's bucket.
        var trend = TrendFor(LocalToday(23, 30).AddDays(-1));

        Assert.Equal(0, trend[^1]);
        Assert.Equal(1, trend[^2]);
    }

    [Fact]
    public void TheTrendWindowHasAStableWidthAndTotalsTheMessagesInIt()
    {
        var snap = OversightRollupBuilder.Build(
            [
                ThreadAt(LocalToday(9, 0)),
                ThreadAt(LocalToday(10, 0)),
                ThreadAt(LocalToday(9, 0).AddDays(-1)),
                ThreadAt(LocalToday(9, 0).AddDays(-2))
            ],
            [Inst("acct")],
            OversightGrouping.ByInstance,
            _ => 15);

        var trend = snap.Entities.Single().TrendCounts;

        Assert.Equal(2, trend[^1]);
        Assert.Equal(1, trend[^2]);
        Assert.Equal(1, trend[^3]);
        Assert.Equal(4, trend.Sum());
    }

    [Fact]
    public void MessagesOlderThanTheWindowAreExcludedRatherThanClampedIntoTheOldestBucket()
    {
        // Clamping would make the oldest bar absorb all history and permanently misreport the trend.
        var trend = TrendFor(LocalToday(9, 0).AddDays(-90));

        Assert.Equal(0, trend.Sum());
    }

    [Fact]
    public void AFutureTimestampDoesNotCorruptTheTrend()
    {
        // Clock skew between the scraped page and the machine is real; a future-dated message must be
        // dropped, not indexed out of range or folded into today.
        var trend = TrendFor(LocalToday(9, 0).AddDays(3));

        Assert.Equal(0, trend.Sum());
    }
}
