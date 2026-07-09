using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class WeeklyActivityChartSummary
{
    public required string SummaryText { get; init; }

    public int TotalSent { get; init; }

    public int TotalReceived { get; init; }

    public int PeakDailyTotal { get; init; }
}

public static class WeeklyActivityChartHelper
{
    public static WeeklyActivityChartSummary BuildSummary(IReadOnlyList<DailyActivityPoint>? series)
    {
        if (series is null || series.Count == 0)
        {
            return new WeeklyActivityChartSummary
            {
                SummaryText = "No activity recorded yet."
            };
        }

        var totalSent = series.Sum(point => point.Sent);
        var totalReceived = series.Sum(point => point.Received);
        var peakDailyTotal = series.Max(point => point.Sent + point.Received);

        return new WeeklyActivityChartSummary
        {
            TotalSent = totalSent,
            TotalReceived = totalReceived,
            PeakDailyTotal = peakDailyTotal,
            SummaryText = $"{totalSent} sent, {totalReceived} received (7-day total)"
        };
    }

    public static int ComputeScaleMaximum(IReadOnlyList<DailyActivityPoint> series) =>
        Math.Max(1, series.Max(point => Math.Max(point.Sent, point.Received)));

    public static double ComputeBarHeight(int value, int scaleMaximum, double maxHeight = 100)
    {
        if (scaleMaximum <= 0)
        {
            return 0;
        }

        if (value <= 0)
        {
            return 0;
        }

        return Math.Max(4, value * maxHeight / scaleMaximum);
    }
}
