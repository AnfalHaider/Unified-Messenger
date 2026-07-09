namespace UnifiedMessenger.Services;

public sealed class MessageVolumeLineChartSummary
{
    public required string SummaryText { get; init; }

    public string LinePathData { get; init; } = string.Empty;

    public string AreaPathData { get; init; } = string.Empty;

    public int PeakTotal { get; init; }

    public int TotalMessages { get; init; }
}

public static class MessageVolumeLineChartHelper
{
    public static MessageVolumeLineChartSummary Build(
        IReadOnlyList<DailyActivityPoint>? series,
        double width = 320,
        double height = 96,
        bool rangeExceedsDisplayCap = false,
        bool isHistoricalMode = false)
    {
        if (series is null || series.Count == 0)
        {
            return new MessageVolumeLineChartSummary
            {
                SummaryText = BuildSummaryText(0, 0, rangeExceedsDisplayCap, isHistoricalMode)
            };
        }

        var totals = series.Select(point => point.Sent + point.Received).ToList();
        var totalMessages = totals.Sum();
        if (totalMessages == 0)
        {
            return new MessageVolumeLineChartSummary
            {
                SummaryText = BuildSummaryText(0, 0, rangeExceedsDisplayCap, isHistoricalMode)
            };
        }

        var peak = totals.Max();
        var stepX = series.Count <= 1 ? width : width / (series.Count - 1);
        var linePoints = new List<(double X, double Y)>(series.Count);

        for (var i = 0; i < series.Count; i++)
        {
            var x = i * stepX;
            var y = height - (totals[i] * height / peak);
            linePoints.Add((x, y));
        }

        var linePath = BuildPolyline(linePoints, closeArea: false);
        var areaPath = BuildArea(linePoints, height);

        return new MessageVolumeLineChartSummary
        {
            SummaryText = BuildSummaryText(totalMessages, peak, rangeExceedsDisplayCap, isHistoricalMode),
            LinePathData = linePath,
            AreaPathData = areaPath,
            PeakTotal = peak,
            TotalMessages = totalMessages
        };
    }

    private static string BuildSummaryText(
        int totalMessages,
        int peakDayTotal,
        bool rangeExceedsDisplayCap,
        bool isHistoricalMode)
    {
        if (totalMessages == 0)
        {
            var kpiHint = isHistoricalMode
                ? "KPI cards reflect threads in the selected period."
                : "KPI cards show your live queue.";
            var empty =
                $"No message volume in the selected range. Volume comes from synced message history; {kpiHint}";
            return rangeExceedsDisplayCap
                ? $"{empty} Chart shows the first {OccDateRangeFilterHelper.ChartDisplayDayCap} days only."
                : empty;
        }

        var summary = $"{totalMessages} messages in range (peak day {peakDayTotal})";
        return rangeExceedsDisplayCap
            ? $"{summary}. Chart shows the first {OccDateRangeFilterHelper.ChartDisplayDayCap} days only."
            : summary;
    }

    private static string BuildPolyline(IReadOnlyList<(double X, double Y)> points, bool closeArea)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append($"M {points[0].X:0.##},{points[0].Y:0.##}");
        for (var i = 1; i < points.Count; i++)
        {
            builder.Append($" L {points[i].X:0.##},{points[i].Y:0.##}");
        }

        if (closeArea)
        {
            builder.Append(" Z");
        }

        return builder.ToString();
    }

    private static string BuildArea(IReadOnlyList<(double X, double Y)> points, double height)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append($"M {points[0].X:0.##},{height:0.##}");
        builder.Append($" L {points[0].X:0.##},{points[0].Y:0.##}");
        for (var i = 1; i < points.Count; i++)
        {
            builder.Append($" L {points[i].X:0.##},{points[i].Y:0.##}");
        }

        builder.Append($" L {points[^1].X:0.##},{height:0.##} Z");
        return builder.ToString();
    }
}
