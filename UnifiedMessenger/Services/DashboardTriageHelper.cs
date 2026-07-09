using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class DashboardTriageHelper
{
    public static string FormatSentimentSummary(MessageTriageDashboardSnapshot snapshot)
    {
        var total = snapshot.PositiveCount + snapshot.NeutralCount + snapshot.NegativeCount;
        if (total == 0)
        {
            return "Awaiting classified messages";
        }

        return $"{snapshot.PositiveCount} positive · {snapshot.NeutralCount} neutral · {snapshot.NegativeCount} negative";
    }

    public static int ComputeSentimentScaleMaximum(IReadOnlyList<DailySentimentPoint> series)
    {
        if (series.Count == 0)
        {
            return 1;
        }

        return Math.Max(1, series.Max(point => point.Positive + point.Neutral + point.Negative));
    }

    public const double MinNonZeroBarHeight = 8;

    public static double ComputeBarHeight(int value, int maxValue, double maxHeight)
    {
        if (value <= 0 || maxValue <= 0)
        {
            return 0;
        }

        var scaled = value * maxHeight / maxValue;
        return Math.Max(MinNonZeroBarHeight, scaled);
    }
}
