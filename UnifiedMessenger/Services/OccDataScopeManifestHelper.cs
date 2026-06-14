using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class OccDataScopeManifestHelper
{
    public static OccDataScopeManifest Build(
        OccViewMode viewMode,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int threadsInQueue,
        int chartThreadCount = 0)
    {
        var chartLabel = OccDateRangeFilterHelper.FormatChartRangeLabel(fromUtc, toUtc);
        var isHistorical = viewMode == OccViewMode.Historical;

        if (!isHistorical)
        {
            return new OccDataScopeManifest
            {
                ViewMode = viewMode,
                ChartRangeLabel = chartLabel,
                QueueScopeLabel = "live now",
                ThreadsInQueue = threadsInQueue,
                SlaCountsAreLive = true,
                ChartUsesAnalyticsStore = true,
                ThreadsFilteredByLastActive = false,
                ShowHistoricalBanner = false,
                BannerTitle = string.Empty,
                BannerBody = $"Chart: {chartLabel} · Queue: current workload (live)"
            };
        }

        var rangeLabel = OccDateRangeFilterHelper.FormatHistoricalRangeLabel(fromUtc, toUtc);
        return new OccDataScopeManifest
        {
            ViewMode = viewMode,
            ChartRangeLabel = chartLabel,
            QueueScopeLabel = $"last active in range ({threadsInQueue})",
            ThreadsInQueue = threadsInQueue,
            SlaCountsAreLive = true,
            ChartUsesAnalyticsStore = true,
            ThreadsFilteredByLastActive = true,
            ShowHistoricalBanner = true,
            BannerTitle = $"Historical report · {rangeLabel}",
            BannerBody =
                $"Threads: last active in range ({threadsInQueue}) · Chart: analytics store · " +
                $"SLA breaches: live state as of now · Not re-scraped from WhatsApp"
        };
    }
}
