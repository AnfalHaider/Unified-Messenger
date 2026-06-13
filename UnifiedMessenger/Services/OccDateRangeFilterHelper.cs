using System.Globalization;

namespace UnifiedMessenger.Services;

using UnifiedMessenger.Models;

public static class OccDateRangeFilterHelper
{
    public const int ChartDisplayDayCap = 31;

    public static bool IsWithinRange(
        DateTimeOffset value,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (fromUtc is { } from && value < from)
        {
            return false;
        }

        if (toUtc is { } to && value > to)
        {
            return false;
        }

        return true;
    }

    public static IEnumerable<T> FilterByTimestamp<T>(
        IEnumerable<T> source,
        Func<T, DateTimeOffset> timestampSelector,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (fromUtc is null && toUtc is null)
        {
            return source;
        }

        return source.Where(item => IsWithinRange(timestampSelector(item), fromUtc, toUtc));
    }

    public static IReadOnlyList<DailyActivityPoint> BuildDailySeriesForRange(
        IReadOnlyDictionary<string, int> sentByDay,
        IReadOnlyDictionary<string, int> receivedByDay,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        var start = (fromUtc ?? DateTimeOffset.Now.AddDays(-(OccDateRangeFilterState.DefaultWindowDays - 1))).LocalDateTime.Date;
        var end = (toUtc ?? DateTimeOffset.Now).LocalDateTime.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var spanDays = Math.Min(ChartDisplayDayCap, (end - start).Days + 1);
        var points = new List<DailyActivityPoint>(spanDays);
        for (var offset = 0; offset < spanDays; offset++)
        {
            var date = start.AddDays(offset);
            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sentByDay.TryGetValue(key, out var sent);
            receivedByDay.TryGetValue(key, out var received);
            points.Add(new DailyActivityPoint
            {
                Label = date.Date == DateTime.Now.Date ? "Today" : date.ToString("MMM d", CultureInfo.CurrentCulture),
                Sent = sent,
                Received = received
            });
        }

        return points;
    }

    public static bool ExceedsChartDisplayCap(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        var start = (fromUtc ?? DateTimeOffset.Now.AddDays(-(OccDateRangeFilterState.DefaultWindowDays - 1))).LocalDateTime.Date;
        var end = (toUtc ?? DateTimeOffset.Now).LocalDateTime.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        return (end - start).Days + 1 > ChartDisplayDayCap;
    }

    public static string FormatScopeLabel(
        string branchScope,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        OccViewMode viewMode = OccViewMode.Live)
    {
        var modeLabel = viewMode == OccViewMode.Historical ? "Historical report" : "Live workload";
        var rangeLabel = FormatRangeLabel(fromUtc, toUtc);
        if (string.IsNullOrWhiteSpace(rangeLabel))
        {
            return $"{branchScope} · {modeLabel}";
        }

        return $"{branchScope} · {modeLabel} · {rangeLabel}";
    }

    public static string FormatRangeLabel(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        if (fromUtc is null && toUtc is null)
        {
            return string.Empty;
        }

        var from = (fromUtc ?? DateTimeOffset.Now.AddDays(-(OccDateRangeFilterState.DefaultWindowDays - 1))).LocalDateTime.Date;
        var to = (toUtc ?? DateTimeOffset.Now).LocalDateTime.Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var culture = CultureInfo.CurrentCulture;
        if (from == to)
        {
            return from.ToString("MMM d, yyyy", culture);
        }

        if (from.Year == to.Year)
        {
            return $"{from.ToString("MMM d", culture)} – {to.ToString("MMM d, yyyy", culture)}";
        }

        return $"{from.ToString("MMM d, yyyy", culture)} – {to.ToString("MMM d, yyyy", culture)}";
    }
}
