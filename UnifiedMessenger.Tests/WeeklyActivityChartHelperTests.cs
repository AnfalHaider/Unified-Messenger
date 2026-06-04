using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WeeklyActivityChartHelperTests
{
    [Fact]
    public void BuildSummary_ReturnsEmptyMessageForNoData()
    {
        var summary = WeeklyActivityChartHelper.BuildSummary([]);

        Assert.Equal("No activity recorded yet.", summary.SummaryText);
    }

    [Fact]
    public void BuildSummary_TotalsSevenDayActivity()
    {
        var summary = WeeklyActivityChartHelper.BuildSummary(
        [
            new DailyActivityPoint { Label = "Mon", Sent = 2, Received = 3 },
            new DailyActivityPoint { Label = "Tue", Sent = 5, Received = 1 }
        ]);

        Assert.Equal(7, summary.TotalSent);
        Assert.Equal(4, summary.TotalReceived);
        Assert.Equal(6, summary.PeakDailyTotal);
        Assert.Contains("7 sent", summary.SummaryText);
    }

    [Theory]
    [InlineData(0, 10, 4)]
    [InlineData(5, 10, 50)]
    public void ComputeBarHeight_UsesScaleMaximum(int value, int scaleMaximum, double expectedMinimum)
    {
        var height = WeeklyActivityChartHelper.ComputeBarHeight(value, scaleMaximum);

        Assert.True(height >= expectedMinimum);
    }
}
