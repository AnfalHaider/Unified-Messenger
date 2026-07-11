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
    public void AllDimensions_CountTheSameTotal_FromTheHourMatrix()
    {
        // Regression for the "18003 vs 18013" bug: hour-of-day summed the day×hour matrix while day-of-week and
        // month summed the separate DailyReceived map, so the totals drifted. Give the two sources DIFFERENT
        // totals for the same day; every dimension must report the matrix's total (10), not the daily map (13).
        var service = new MessageAnalyticsService(_storePath);
        var day = DateTime.Today.ToString("yyyy-MM-dd");
        var hours = new int[24];
        hours[10] = 6;
        hours[14] = 4; // matrix total = 10
        service.RecordBackfillDailyAggregate(
            "inst-1",
            new Dictionary<string, int> { [day] = 13 }, // drifted daily-total map
            new Dictionary<string, int>(),
            null,
            new Dictionary<string, int[]> { [day] = hours });

        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "One", Platform = "whatsapp", AccentColor = "#111111" }
        };

        // Both the combined and the per-account (chart) paths, all three dimensions.
        foreach (var dim in new[] { ActivityDimension.HourOfDay, ActivityDimension.DayOfWeek, ActivityDimension.Month })
        {
            Assert.Equal(10, service.BuildActivityPatterns(dim, instances, null, null).Total);
            Assert.Equal(10, service.BuildActivityBreakdown(dim, instances, null, null).Total);
        }
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
    public void BuildWeekHourHeatmap_BucketsByWeekdayAndHour_AndFindsPeakBlock()
    {
        var service = new MessageAnalyticsService(_storePath);
        // Find a recent Saturday so the day-of-week bucketing is deterministic.
        var saturday = DateTime.Today;
        while (saturday.DayOfWeek != DayOfWeek.Saturday)
        {
            saturday = saturday.AddDays(-1);
        }

        // 5 messages Saturday 7 PM, 1 Monday 9 AM.
        for (var i = 0; i < 5; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(saturday.AddHours(19).AddMinutes(i)));
        }
        var monday = saturday.AddDays(2);
        service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(monday.AddHours(9)));

        var map = service.BuildWeekHourHeatmap([], null, null);

        Assert.True(map.HasData);
        Assert.Equal(6, map.Total);
        Assert.Equal("Saturday", map.BusiestDayName);
        Assert.Equal(19, map.PeakHour);
        Assert.Equal(5, map.PeakValue);
        Assert.Equal(5, map.Grid[5][19]); // Sat=index5, 7 PM
        Assert.Equal(1, map.Grid[0][9]);  // Mon=index0, 9 AM
        // The busiest 3-hour block should include 7 PM and carry the Saturday cluster.
        Assert.InRange(map.BusiestBlockStartHour, 17, 19);
    }

    [Fact]
    public void BuildWeekHourHeatmap_NoData_IsEmpty()
    {
        var service = new MessageAnalyticsService(_storePath);
        Assert.False(service.BuildWeekHourHeatmap([], null, null).HasData);
    }

    [Fact]
    public void BuildActivityBreakdown_SplitsBucketsByAccount()
    {
        var service = new MessageAnalyticsService(_storePath);
        var today = DateTime.Today;

        // inst-1: 3 at 2pm; inst-2: 1 at 2pm, 2 at 9am.
        for (var i = 0; i < 3; i++)
        {
            service.RecordMessageReceived("inst-1", receivedAtUtc: LocalAt(today.AddHours(14).AddMinutes(i)));
        }
        service.RecordMessageReceived("inst-2", receivedAtUtc: LocalAt(today.AddHours(14)));
        service.RecordMessageReceived("inst-2", receivedAtUtc: LocalAt(today.AddHours(9)));
        service.RecordMessageReceived("inst-2", receivedAtUtc: LocalAt(today.AddHours(9).AddMinutes(1)));

        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "One", Platform = "whatsapp", AccentColor = "#111111" },
            new() { Id = "inst-2", DisplayName = "Two", Platform = "whatsapp", AccentColor = "#222222" }
        };

        var b = service.BuildActivityBreakdown(ActivityDimension.HourOfDay, instances, null, null);

        Assert.True(b.HasData);
        Assert.Equal(6, b.Total);
        Assert.Equal(2, b.Series.Count);
        Assert.Equal(4, b.Totals[14]); // 3 + 1 at 2pm
        Assert.Equal(2, b.Totals[9]);
        // Busiest account (inst-1 with 3) sorts first.
        Assert.Equal("inst-1", b.Series[0].InstanceId);
        Assert.Equal(3, b.Series[0].Values[14]);
        Assert.Equal(1, b.Series[1].Values[14]);
        Assert.Equal(2, b.Series[1].Values[9]);
    }

    [Fact]
    public void ChartPalette_SamePlatformAccents_GetDistinctColours()
    {
        // Three WhatsApp accounts all carry the same brand green — the stacked chart must not be monochrome.
        var series = new List<ActivityAccountSeries>
        {
            new() { InstanceId = "b", DisplayName = "B", AccentColor = "#25D366", Values = new int[24], Total = 5 },
            new() { InstanceId = "a", DisplayName = "A", AccentColor = "#25D366", Values = new int[24], Total = 9 },
            new() { InstanceId = "c", DisplayName = "C", AccentColor = "#25D366", Values = new int[24], Total = 2 }
        };

        var colors = ChartPalette.ResolveSeriesColors(series);

        Assert.Equal(3, colors.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Stable by instance id, not by series order.
        Assert.Equal(ChartPalette.Palette[0], colors["a"]);
        Assert.Equal(ChartPalette.Palette[1], colors["b"]);
        Assert.Equal(ChartPalette.Palette[2], colors["c"]);
    }

    [Fact]
    public void ChartPalette_DistinctAccents_AreKept()
    {
        var series = new List<ActivityAccountSeries>
        {
            new() { InstanceId = "a", DisplayName = "A", AccentColor = "#111111", Values = new int[7], Total = 1 },
            new() { InstanceId = "b", DisplayName = "B", AccentColor = "#222222", Values = new int[7], Total = 1 }
        };

        var colors = ChartPalette.ResolveSeriesColors(series);

        Assert.Equal("#111111", colors["a"]);
        Assert.Equal("#222222", colors["b"]);
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
