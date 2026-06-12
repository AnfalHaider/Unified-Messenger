namespace UnifiedMessenger.Services;

public static class OccDateRangeFilterHelper
{
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
        var start = (fromUtc ?? DateTimeOffset.Now.AddDays(-6)).LocalDateTime.Date;
        var end = (toUtc ?? DateTimeOffset.Now).LocalDateTime.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var spanDays = Math.Min(31, (end - start).Days + 1);
        var points = new List<DailyActivityPoint>(spanDays);
        for (var offset = 0; offset < spanDays; offset++)
        {
            var date = start.AddDays(offset);
            var key = date.ToString("yyyy-MM-dd");
            sentByDay.TryGetValue(key, out var sent);
            receivedByDay.TryGetValue(key, out var received);
            points.Add(new DailyActivityPoint
            {
                Label = date.Date == DateTime.Now.Date ? "Today" : date.ToString("MMM d"),
                Sent = sent,
                Received = received
            });
        }

        return points;
    }
}
