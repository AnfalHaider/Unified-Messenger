using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// DST boundary coverage for the daily-bucketing metric paths: the command-centre sparkline
/// (<c>OversightRollupBuilder.BuildTrend</c>) and the two response-time day series
/// (<c>ResponseTimeTracker.GetDailyMedians</c> / <c>GetDailyWithinThreshold</c>), plus the
/// answered-today counter.
///
/// <para>
/// <b>What this is checking for.</b> Daily buckets go wrong across a transition in one of two ways.
/// Either the code derives "how many days ago" from an elapsed <see cref="TimeSpan"/>, in which case a
/// 23-hour day rounds to zero days and two calendar days collapse into one bar; or it builds the day key
/// from something other than a genuine local conversion, in which case messages near midnight land on
/// the wrong side. Both produce a chart that is quietly wrong for a day and then silently corrects
/// itself, which is close to undiagnosable from a support ticket.
/// </para>
/// <para>
/// <b>Result: both paths are clean.</b> They key on calendar dates converted through the real zone and
/// subtract dates rather than durations, which is DST-immune. That was true before this audit — these
/// tests exist to record it and to keep it true, because the arithmetic that makes it work is not
/// obvious and an innocuous-looking edit to a duration would break it. The zone parameters these tests
/// use were added for exactly this purpose: this machine and CI both sit in zones with no DST, so
/// without an injectable zone the assertions would pass against broken code.
/// </para>
/// </summary>
public class DstMetricBucketingTests
{
    private static readonly TimeZoneInfo Zone = DstTimeZones.TwoAmTransition;

    private static MessengerInstance Inst(string id) =>
        new() { Id = id, DisplayName = id, ProfileName = id, Platform = "whatsapp" };

    /// <summary>A wall-clock time in the DST fixture zone, resolved to the correct absolute instant.</summary>
    private static DateTimeOffset At(DateTime localWallClock)
    {
        var unspecified = DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified);
        var offset = Zone.IsAmbiguousTime(unspecified)
            ? Zone.GetAmbiguousTimeOffsets(unspecified).Max()
            : Zone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }

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

    private static IReadOnlyList<int> Trend(DateTimeOffset nowUtc, params DateTimeOffset[] messageTimes) =>
        OversightRollupBuilder.Build(
            messageTimes.Select(ThreadAt).ToList(),
            [Inst("acct")],
            OversightGrouping.ByInstance,
            _ => 15,
            nowUtc: nowUtc,
            zone: Zone).Entities.Single().TrendCounts;

    // ---- Sparkline: the 23-hour day -----------------------------------------------------------------

    [Fact]
    public void TheSparklineKeepsTheSpringForwardDayAndTheDayBeforeItInSeparateBars()
    {
        // 8 March 2026 is 23 hours long in this zone. If "days ago" were computed from elapsed time,
        // 7 March 09:00 would be 23 hours before 8 March 08:00 → 0 days → both would land in today's bar.
        var now = At(DstTimeZones.SpringForwardDay.AddHours(20));
        var trend = Trend(now, At(DstTimeZones.SpringForwardDay.AddHours(9)), At(new DateTime(2026, 3, 7, 9, 0, 0)));

        Assert.Equal(1, trend[^1]);
        Assert.Equal(1, trend[^2]);
        Assert.Equal(2, trend.Sum());
    }

    [Fact]
    public void AMessageInTheHourBeforeASpringForwardIsStillYesterdayNotToday()
    {
        // 7 March 23:30 is only ~8.5 hours before 8 March 08:00 because of the skipped hour. It must
        // still be attributed to the 7th.
        var now = At(DstTimeZones.SpringForwardDay.AddHours(8));
        var trend = Trend(now, At(new DateTime(2026, 3, 7, 23, 30, 0)));

        Assert.Equal(0, trend[^1]);
        Assert.Equal(1, trend[^2]);
    }

    [Fact]
    public void TheDayAfterASpringForwardStillSeesTheShortDayAsASeparateBar()
    {
        // The sharpest case, and the one the other tests miss: view the chart on 9 March, when the
        // 23-hour day is *behind* today. Only 23 hours separate the start of the 8th from the start of
        // the 9th, so anything that converts that gap to whole days truncates it to zero and folds the
        // 8th's messages into today's bar — today reads double and yesterday reads empty.
        //
        // Verified by injecting exactly that arithmetic: with `(int)(startOfToday - startOfThatDay)
        // .TotalDays` in place of the calendar subtraction, this test fails and the rest still pass.
        var dayAfter = DstTimeZones.SpringForwardDay.AddDays(1);
        var trend = Trend(
            At(dayAfter.AddHours(12)),
            At(dayAfter.AddHours(9)),
            At(DstTimeZones.SpringForwardDay.AddHours(9)),
            At(DstTimeZones.SpringForwardDay.AddDays(-1).AddHours(9)));

        Assert.Equal(1, trend[^1]);
        Assert.Equal(1, trend[^2]);
        Assert.Equal(1, trend[^3]);
        Assert.Equal(3, trend.Sum());
    }

    [Fact]
    public void AMessageInTheHourAfterASpringForwardIsToday()
    {
        // 03:00 local on the transition day — the first hour that exists after the clocks jump.
        var now = At(DstTimeZones.SpringForwardDay.AddHours(20));
        var trend = Trend(now, At(DstTimeZones.SpringForwardDay.AddHours(3)));

        Assert.Equal(1, trend[^1]);
    }

    // ---- Sparkline: the 25-hour day -----------------------------------------------------------------

    [Fact]
    public void BothOccurrencesOfTheRepeatedHourCountAsTheSameDay()
    {
        // 01:30 happens twice on 1 November. Both are the 1st; neither may leak into the 31st or the 2nd.
        var ambiguous = DateTime.SpecifyKind(DstTimeZones.FallBackDay.AddHours(1).AddMinutes(30), DateTimeKind.Unspecified);
        var offsets = Zone.GetAmbiguousTimeOffsets(ambiguous);
        Assert.Equal(2, offsets.Length);

        var now = At(DstTimeZones.FallBackDay.AddHours(20));
        var trend = Trend(
            now,
            new DateTimeOffset(ambiguous, offsets.Max()),
            new DateTimeOffset(ambiguous, offsets.Min()));

        Assert.Equal(2, trend[^1]);
        Assert.Equal(2, trend.Sum());
    }

    [Fact]
    public void TheSparklineKeepsTheFallBackDayAndTheDayAfterItInSeparateBars()
    {
        // Mirror of the spring case: 1 November 23:00 and 2 November 00:30 are only 1.5 hours apart in
        // elapsed terms... but 25 hours of calendar day separate 1 Nov 00:30 from 2 Nov 00:30.
        var now = At(new DateTime(2026, 11, 2, 12, 0, 0));
        var trend = Trend(now, At(DstTimeZones.FallBackDay.AddHours(23)), At(new DateTime(2026, 11, 2, 0, 30, 0)));

        Assert.Equal(1, trend[^1]);
        Assert.Equal(1, trend[^2]);
    }

    [Fact]
    public void SevenBarsStillCoverSevenCalendarDaysWhenATransitionFallsInside()
    {
        // One message on each of the seven days ending on the fall-back day. All seven must be visible;
        // a duration-based window would have shifted one out of range.
        var now = At(DstTimeZones.FallBackDay.AddHours(22));
        var days = Enumerable.Range(0, 7)
            .Select(i => At(DstTimeZones.FallBackDay.AddDays(-i).AddHours(12)))
            .ToArray();

        var trend = Trend(now, days);

        Assert.Equal(7, trend.Count);
        Assert.All(trend, bar => Assert.Equal(1, bar));
    }

    // ---- Response-time day series -------------------------------------------------------------------

    private static ResponseTimeTracker TrackerWithSamples(string storePath, params DateTimeOffset[] answeredAt)
    {
        var tracker = new ResponseTimeTracker(storePath);
        tracker.SetWatchStartForTests("acct", new DateTimeOffset(new DateTime(2020, 1, 1), TimeSpan.Zero));
        foreach (var answered in answeredAt)
        {
            // Inbound 10 minutes before the reply, so each pair records one 10-minute FRT sample keyed to
            // the reply's local day.
            var inbound = answered.AddMinutes(-10);
            var key = Guid.NewGuid().ToString("N");
            tracker.Observe("acct", key, isAwaiting: true, lastMessageFromMe: false, inbound);
            tracker.Observe("acct", key, isAwaiting: false, lastMessageFromMe: true, answered);
        }

        return tracker;
    }

    private static string TempStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "response-times.json");
    }

    // These walk backwards from the real clock, so they use the most recent transition already in the
    // past rather than the fixed 2026 dates the sparkline tests use (which inject their own "now").
    private const int Lookback = DstTimeZones.LookbackDaysCoveringATransition;

    [Fact]
    public void ResponseTimeDaySeriesSeparatesTheTransitionDayFromItsNeighbours()
    {
        // Two replies either side of the spring-forward boundary. They are 23-and-a-bit hours apart, so
        // any elapsed-time bucketing would file them on the same day.
        var spring = DstTimeZones.LatestPastSpringForward();
        var tracker = TrackerWithSamples(
            TempStore(),
            At(spring.AddDays(-1).AddHours(22)),
            At(spring.AddHours(22)));

        var byDay = tracker.GetDailyMedians([Inst("acct")], days: Lookback, zone: Zone)
            .Where(p => p.Count > 0)
            .ToList();

        Assert.Equal(2, byDay.Count);
        Assert.All(byDay, p => Assert.Equal(1, p.Count));
    }

    [Fact]
    public void RepliesInTheRepeatedHourAllLandOnTheFallBackDay()
    {
        var autumn = DstTimeZones.LatestPastFallBack();
        var ambiguous = DateTime.SpecifyKind(autumn.AddHours(1).AddMinutes(30), DateTimeKind.Unspecified);
        var offsets = Zone.GetAmbiguousTimeOffsets(ambiguous);
        Assert.Equal(2, offsets.Length);

        var tracker = TrackerWithSamples(
            TempStore(),
            new DateTimeOffset(ambiguous, offsets.Max()),
            new DateTimeOffset(ambiguous, offsets.Min()));

        var populated = tracker.GetDailyWithinThreshold([Inst("acct")], thresholdMinutes: 15, days: Lookback, zone: Zone)
            .Where(p => p.Count > 0)
            .ToList();

        var single = Assert.Single(populated);
        Assert.Equal(2, single.Count);
        Assert.Equal(100, single.Percent); // both 10-minute replies are inside the 15-minute threshold
    }

    [Fact]
    public void TheDaySeriesReturnsTheRequestedNumberOfDistinctDaysAcrossATransition()
    {
        // The labelled x-axis must not repeat or skip a day because one of them was 23 or 25 hours long.
        var tracker = TrackerWithSamples(TempStore());
        var series = tracker.GetDailyMedians([Inst("acct")], days: 7, zone: Zone);

        Assert.Equal(7, series.Count);
        Assert.Equal(7, series.Select(p => p.Label).Distinct().Count());
    }

    [Fact]
    public void The25HourDayHoldsAllOfItsRepliesAndNoneOfItsNeighboursDo()
    {
        // The same local-date comparison that AnsweredToday uses, exercised where it can be pinned: all
        // 25 hours belong to the transition day, and the days either side keep their own.
        var autumn = DstTimeZones.LatestPastFallBack();
        var tracker = TrackerWithSamples(
            TempStore(),
            At(autumn.AddMinutes(30)),                    // 00:30, before the fall back
            At(autumn.AddHours(23)),                      // 23:00, after it
            At(autumn.AddDays(-1).AddHours(23).AddMinutes(30)),
            At(autumn.AddDays(1).AddMinutes(30)));

        var series = tracker.GetDailyWithinThreshold([Inst("acct")], thresholdMinutes: 15, days: Lookback, zone: Zone)
            .Where(p => p.Count > 0)
            .ToList();

        Assert.Equal(3, series.Count);              // the day before, the 25-hour day, the day after
        Assert.Contains(series, p => p.Count == 2); // both of the transition day's replies on one day
        Assert.Equal(4, series.Sum(p => p.Count));
    }

    // ---- Analytics daily + hourly buckets ----------------------------------------------------------

    private static MessageAnalyticsService NewAnalytics()
    {
        var dir = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new MessageAnalyticsService(Path.Combine(dir, "analytics.json"), Zone);
    }

    private const int Sat = 5;
    private const int Sun = 6;
    private const int Mon = 0;

    [Fact]
    public void AnalyticsFilesEachMessageOnTheLocalDayAndLocalHourItReallyArrived()
    {
        // Both fixture transitions fall on a Sunday. Walk the boundary: last night, the hour before the
        // clocks jump, the first hour after, that evening, and the next morning. Each must land on its
        // own weekday and its own wall-clock hour — the hour is the part a fixed offset would get wrong,
        // because 03:30 on the transition day is a different offset from 01:30 on the same day.
        var spring = DstTimeZones.LatestPastSpringForward();
        Assert.Equal(DayOfWeek.Sunday, spring.DayOfWeek);

        var analytics = NewAnalytics();
        foreach (var moment in new[]
                 {
                     spring.AddDays(-1).AddHours(23).AddMinutes(30), // Sat 23:30
                     spring.AddHours(1).AddMinutes(30),              // Sun 01:30, before the jump
                     spring.AddHours(3).AddMinutes(30),              // Sun 03:30, after it
                     spring.AddHours(23).AddMinutes(30),             // Sun 23:30
                     spring.AddDays(1).AddMinutes(30)                // Mon 00:30
                 })
        {
            analytics.RecordMessageReceived("inst-1", receivedAtUtc: At(moment));
        }

        var grid = analytics.BuildWeekHourHeatmap([], null, null).Grid;

        Assert.Equal(1, grid[Sat][23]);
        Assert.Equal(1, grid[Sun][1]);
        Assert.Equal(1, grid[Sun][3]);
        Assert.Equal(1, grid[Sun][23]);
        Assert.Equal(1, grid[Mon][0]);

        // 02:00 never existed on this day, so nothing may be filed there.
        Assert.Equal(0, grid[Sun][2]);
    }

    [Fact]
    public void BothHalvesOfARepeatedHourAreCountedOnTheSameDayAndHour()
    {
        // On the 25-hour day 01:30 happens twice. Both are Sunday 01:30 — two messages in one cell, not
        // one lost and not one pushed into Saturday.
        var autumn = DstTimeZones.LatestPastFallBack();
        Assert.Equal(DayOfWeek.Sunday, autumn.DayOfWeek);

        var ambiguous = DateTime.SpecifyKind(autumn.AddHours(1).AddMinutes(30), DateTimeKind.Unspecified);
        var offsets = Zone.GetAmbiguousTimeOffsets(ambiguous);

        var analytics = NewAnalytics();
        analytics.RecordMessageReceived("inst-1", receivedAtUtc: new DateTimeOffset(ambiguous, offsets.Max()));
        analytics.RecordMessageReceived("inst-1", receivedAtUtc: new DateTimeOffset(ambiguous, offsets.Min()));

        var heatmap = analytics.BuildWeekHourHeatmap([], null, null);

        Assert.Equal(2, heatmap.Grid[Sun][1]);
        Assert.Equal(2, heatmap.Total);
        Assert.Equal(0, heatmap.Grid[Sat][1]);
    }

    [Fact]
    public void AnalyticsDailyTotalsSplitTheTransitionDayFromItsNeighbours()
    {
        // The daily buckets behind Messages/day and the week-over-week trend, checked the same way: three
        // calendar days, three buckets, even though only 23 hours separate two of the boundaries.
        var spring = DstTimeZones.LatestPastSpringForward();
        var analytics = NewAnalytics();

        analytics.RecordMessageReceived("inst-1", receivedAtUtc: At(spring.AddDays(-1).AddHours(12)));
        analytics.RecordMessageReceived("inst-1", receivedAtUtc: At(spring.AddHours(12)));
        analytics.RecordMessageReceived("inst-1", receivedAtUtc: At(spring.AddHours(20)));
        analytics.RecordMessageReceived("inst-1", receivedAtUtc: At(spring.AddDays(1).AddHours(12)));

        var byWeekday = analytics.BuildActivityPatterns(ActivityDimension.DayOfWeek, [], null, null);

        Assert.Equal(1, byWeekday.Values[Sat]);
        Assert.Equal(2, byWeekday.Values[Sun]);
        Assert.Equal(1, byWeekday.Values[Mon]);
        Assert.Equal(4, byWeekday.Total);
    }
}
