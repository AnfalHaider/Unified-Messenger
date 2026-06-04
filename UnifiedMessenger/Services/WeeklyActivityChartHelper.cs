namespace UnifiedMessenger.Services;

public sealed class WeeklyActivitySummary
{
    public int TotalSent { get; init; }

    public int TotalReceived { get; init; }

    public int PeakDailyTotal { get; init; }

    public string SummaryText { get; init; } = string.Empty;
}

public static class WeeklyActivityChartHelper
{
    public static WeeklyActivitySummary BuildSummary(IReadOnlyList<DailyActivityPoint>? series)
    {
        if (series is null || series.Count == 0)
        {
            return new WeeklyActivitySummary
            {
                SummaryText = "No activity recorded yet."
            };
        }

        var totalSent = series.Sum(point => point.Sent);
        var totalReceived = series.Sum(point => point.Received);
        var peakDailyTotal = series.Max(point => point.Sent + point.Received);

        return new WeeklyActivitySummary
        {
            TotalSent = totalSent,
            TotalReceived = totalReceived,
            PeakDailyTotal = peakDailyTotal,
            SummaryText = $"{totalSent} sent · {totalReceived} received · peak day {peakDailyTotal}"
        };
    }

    public static int ComputeScaleMaximum(IReadOnlyList<DailyActivityPoint> series) =>
        Math.Max(1, series.Max(point => Math.Max(point.Sent, point.Received)));

    public static double ComputeBarHeight(int value, int scaleMaximum, double maxHeight = 100) =>
        value <= 0 ? 4 : Math.Max(4, maxHeight * value / scaleMaximum);
}
