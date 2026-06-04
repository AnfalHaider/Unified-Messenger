using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardTriageHelperTests
{
    [Fact]
    public void FormatSentimentSummary_ReturnsAwaitingWhenEmpty()
    {
        var text = DashboardTriageHelper.FormatSentimentSummary(MessageTriageDashboardSnapshot.Empty);
        Assert.Contains("Awaiting", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputeSentimentScaleMaximum_ReturnsAtLeastOne()
    {
        var max = DashboardTriageHelper.ComputeSentimentScaleMaximum(
        [
            new DailySentimentPoint { Label = "Mon", Positive = 2, Neutral = 1, Negative = 0 }
        ]);

        Assert.Equal(3, max);
    }
}
