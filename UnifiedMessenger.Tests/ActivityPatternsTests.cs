using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Locks the activity-patterns query logic that feeds the dashboard graph + Messages/day &amp; Busiest-window
/// KPIs: hour-of-day / day-of-week / month bucketing, peak detection, and the per-day average + delta.
/// </summary>
public class ActivityPatternsTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public ActivityPatternsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "analytics.json");
    }

    private static DateTimeOffset LocalAt(DateTime localDateTime) =>
        new(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime));

    [Fact]
    public void BuildActivityPatterns_HourOfDay_HasPeakAtBusiestHour()
    {
        var service = new MessageAnalyticsService(_storePath);
        var today = DateTime.Today;
        for (var i = 0; i < 5; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddHours(14).AddMinutes(i)));
        }
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddHours(9)));

        var result = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], null, null);

        Assert.Equal(24, result.Labels.Count);
        Assert.Equal(24, result.Values.Count);
        Assert.Equal(14, result.PeakIndex);
        Assert.Equal(5, result.Values[14]);
        Assert.Equal(6, result.Total);
        Assert.True(result.HasData);
    }

    [Fact]
    public void BuildActivityPatterns_DayOfWeek_PeaksOnBusiestWeekday()
    {
        var service = new MessageAnalyticsService(_storePath);
        var tuesday = DateTime.Today;
        while (tuesday.DayOfWeek != DayOfWeek.Tuesday)
        {
            tuesday = tuesday.AddDays(-1);
        }
        var monday = tuesday.AddDays(-1);

        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(tuesday.AddHours(10)));
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(tuesday.AddHours(11)));
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(tuesday.AddHours(12)));
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(monday.AddHours(10)));

        var result = service.BuildActivityPatterns(ActivityDimension.DayOfWeek, [], null, null);

        Assert.Equal(7, result.Values.Count);
        Assert.Equal("Mon", result.Labels[0]);
        Assert.Equal(1, result.PeakIndex); // Tuesday
        Assert.Equal("Tuesday", result.PeakLabel);
    }

    [Fact]
    public void BuildActivityPatterns_Month_BucketsByCalendarMonth()
    {
        var service = new MessageAnalyticsService(_storePath);
        var year = DateTime.Today.Year;
        // March (index 2): 3 received; January (index 0): 1.
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(new DateTime(year, 3, 5, 9, 0, 0)));
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(new DateTime(year, 3, 6, 9, 0, 0)));
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(new DateTime(year, 3, 7, 9, 0, 0)));
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(new DateTime(year, 1, 9, 9, 0, 0)));

        var result = service.BuildActivityPatterns(ActivityDimension.Month, [], null, null);

        Assert.Equal(12, result.Values.Count);
        Assert.Equal(2, result.PeakIndex); // March
        Assert.Equal("March", result.PeakLabel);
        Assert.Equal(3, result.Values[2]);
        Assert.Equal(1, result.Values[0]);
    }

    [Fact]
    public void BuildActivityPatterns_NoData_ReturnsEmptyPattern()
    {
        var service = new MessageAnalyticsService(_storePath);
        var result = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], null, null);

        Assert.False(result.HasData);
        Assert.Equal(-1, result.PeakIndex);
        Assert.Equal("—", result.PeakLabel);
    }

    [Fact]
    public void GetMessagesPerDay_AveragesOverWeekAndReportsDelta()
    {
        var service = new MessageAnalyticsService(_storePath);
        var today = DateTime.Today;

        // 14 received across the current 7-day window → avg 2/day.
        for (var i = 0; i < 14; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddHours(10).AddMinutes(i)));
        }
        // 7 received in the prior 7-day window (8 days ago) → prior avg 1/day.
        for (var i = 0; i < 7; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddDays(-8).AddHours(10).AddMinutes(i)));
        }

        var stat = service.GetMessagesPerDay([]);

        Assert.True(stat.HasData);
        Assert.Equal(2, stat.AveragePerDay);
        Assert.Equal(1, stat.DeltaCount); // 2/day now vs 1/day prior
    }

    [Fact]
    public void GetWeekOverWeek_ComparesThisWeekVsLast()
    {
        var service = new MessageAnalyticsService(_storePath);
        var today = DateTime.Today;

        // 6 received this week (today), 2 last week (8 days ago).
        for (var i = 0; i < 6; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddHours(10).AddMinutes(i)));
        }
        for (var i = 0; i < 2; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddDays(-8).AddHours(10).AddMinutes(i)));
        }

        var wow = service.GetWeekOverWeek([]);

        Assert.True(wow.HasData);
        Assert.Equal(6, wow.ThisWeekTotal);
        Assert.Equal(2, wow.LastWeekTotal);
        Assert.Equal(200, wow.DeltaPercent); // (6-2)/2 = +200%
        Assert.Equal(today.DayOfWeek.ToString(), wow.BusiestDay);
    }

    [Fact]
    public void GetEndOfDayProjection_ReportsTodaySoFarAndProjectsForward()
    {
        var service = new MessageAnalyticsService(_storePath);
        var now = DateTime.Now;
        // 5 received earlier today (seconds ago → same day + hour, robust regardless of run time).
        for (var i = 0; i < 5; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(now.AddSeconds(-i)));
        }

        var projection = service.GetEndOfDayProjection([]);

        Assert.True(projection.HasData);
        Assert.Equal(5, projection.SoFar);
        Assert.True(projection.Projected >= projection.SoFar);
    }

    [Fact]
    public void GetEndOfDayProjection_NoActivityToday_HasNoData()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(DateTime.Today.AddDays(-3).AddHours(10)));

        var projection = service.GetEndOfDayProjection([]);

        Assert.False(projection.HasData);
    }

    [Fact]
    public void RecordBackfillDailyAggregate_SetsAuthoritativeHourlyHistogram()
    {
        var service = new MessageAnalyticsService(_storePath);
        var hourly = new int[24];
        hourly[15] = 12;
        hourly[12] = 3;

        service.RecordBackfillDailyAggregate(
            "inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), hourly);

        var result = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], null, null);
        Assert.Equal(12, result.Values[15]);
        Assert.Equal(3, result.Values[12]);
        Assert.Equal(15, result.PeakIndex);
    }

    [Fact]
    public void RecordBackfillDailyAggregate_HourlyReplacesNotAccumulates()
    {
        var service = new MessageAnalyticsService(_storePath);
        var first = new int[24];
        first[15] = 12;
        var second = new int[24];
        second[15] = 20; // a later re-sync sees more messages at 3 PM

        service.RecordBackfillDailyAggregate("inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), first);
        service.RecordBackfillDailyAggregate("inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), second);

        var result = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], null, null);
        Assert.Equal(20, result.Values[15]); // replaced with the fresh count, not 32
    }

    [Fact]
    public void RecordBackfillDailyAggregate_EmptyHourly_LeavesExistingHistogram()
    {
        var service = new MessageAnalyticsService(_storePath);
        var hourly = new int[24];
        hourly[15] = 9;
        service.RecordBackfillDailyAggregate("inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), hourly);

        // A later read where the account wasn't loaded (all-zero hourly) must NOT wipe the histogram.
        service.RecordBackfillDailyAggregate("inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), new int[24]);

        var result = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], null, null);
        Assert.Equal(9, result.Values[15]);
    }

    [Fact]
    public void HourOfDay_DayHourMatrix_HonoursDateRange()
    {
        var service = new MessageAnalyticsService(_storePath);
        var today = DateTime.Today;
        string Day(int daysAgo) => today.AddDays(-daysAgo).ToString("yyyy-MM-dd");

        int[] Hours(int hour, int count)
        {
            var a = new int[24];
            a[hour] = count;
            return a;
        }

        // 10 messages at 3pm today; 7 messages at 9am 60 days ago.
        var matrix = new Dictionary<string, int[]>
        {
            [Day(0)] = Hours(15, 10),
            [Day(60)] = Hours(9, 7)
        };
        service.RecordBackfillDailyAggregate(
            "inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), null, matrix);

        var last30 = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], DateTimeOffset.Now.AddDays(-30), null);
        Assert.Equal(10, last30.Values[15]); // today's is in range
        Assert.Equal(0, last30.Values[9]);   // 60 days ago is NOT in the last-30 window

        var allTime = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], null, null);
        Assert.Equal(10, allTime.Values[15]);
        Assert.Equal(7, allTime.Values[9]);  // all-time includes both
    }

    [Fact]
    public void HourOfDay_DayHourMatrix_ReplacesNotAccumulatesAcrossResyncs()
    {
        var service = new MessageAnalyticsService(_storePath);
        var day = DateTime.Today.ToString("yyyy-MM-dd");
        int[] At(int h, int c) { var a = new int[24]; a[h] = c; return a; }

        service.RecordBackfillDailyAggregate("inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), null, new Dictionary<string, int[]> { [day] = At(15, 5) });
        service.RecordBackfillDailyAggregate("inst-1", new Dictionary<string, int>(), new Dictionary<string, int>(), null, new Dictionary<string, int[]> { [day] = At(15, 8) });

        var result = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [], null, null);
        Assert.Equal(8, result.Values[15]); // the message store is authoritative — replaced, not 13
    }

    [Fact]
    public void BuildActivityPatterns_FiltersByAccount()
    {
        var service = new MessageAnalyticsService(_storePath);
        var today = DateTime.Today;
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddHours(8)));
        service.RecordMessageReceived("inst-2", receivedAtUtc: LocalAt(today.AddHours(8)));
        service.RecordMessageReceived("inst-2", receivedAtUtc: LocalAt(today.AddHours(8).AddMinutes(1)));

        var instance = new MessengerInstance { Id = "inst-2", DisplayName = "Two", Platform = "whatsapp" };
        var result = service.BuildActivityPatterns(ActivityDimension.HourOfDay, [instance], null, null);

        Assert.Equal(2, result.Total); // only inst-2's two messages
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
