using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class MessageVolumeLineChartHelperTests
{
    [Fact]
    public void Build_ReturnsPathDataForSeries()
    {
        var series = new[]
        {
            new DailyActivityPoint { Label = "Mon", Sent = 1, Received = 2 },
            new DailyActivityPoint { Label = "Tue", Sent = 3, Received = 1 }
        };

        var chart = MessageVolumeLineChartHelper.Build(series);

        Assert.False(string.IsNullOrWhiteSpace(chart.LinePathData));
        Assert.False(string.IsNullOrWhiteSpace(chart.AreaPathData));
        Assert.Equal(7, chart.TotalMessages);
        Assert.Contains("7 messages", chart.SummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithZeroTotals_DoesNotReportFakePeakDay()
    {
        var series = new[]
        {
            new DailyActivityPoint { Label = "Mon", Sent = 0, Received = 0 },
            new DailyActivityPoint { Label = "Tue", Sent = 0, Received = 0 }
        };

        var chart = MessageVolumeLineChartHelper.Build(series);

        Assert.Equal(0, chart.TotalMessages);
        Assert.Equal(0, chart.PeakTotal);
        Assert.Contains("No message volume", chart.SummaryText, StringComparison.Ordinal);
        Assert.DoesNotContain("peak day 1", chart.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(chart.LinePathData));
    }

    [Fact]
    public void Build_WarnsWhenRangeExceedsDisplayCap()
    {
        var series = new[]
        {
            new DailyActivityPoint { Label = "Mon", Sent = 1, Received = 1 }
        };

        var chart = MessageVolumeLineChartHelper.Build(series, rangeExceedsDisplayCap: true);

        Assert.Contains("first 31 days", chart.SummaryText, StringComparison.OrdinalIgnoreCase);
    }
}
