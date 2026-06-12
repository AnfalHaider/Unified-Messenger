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
}
