namespace UnifiedMessenger.Models;

/// <summary>
/// Honest data contract for Operations Command Center live vs historical modes.
/// </summary>
public sealed class OccDataScopeManifest
{
    public OccViewMode ViewMode { get; init; }

    public string ChartRangeLabel { get; init; } = string.Empty;

    public string QueueScopeLabel { get; init; } = string.Empty;

    public int ThreadsInQueue { get; init; }

    public bool SlaCountsAreLive { get; init; }

    public bool ChartUsesAnalyticsStore { get; init; }

    public bool ThreadsFilteredByLastActive { get; init; }

    public string BannerTitle { get; init; } = string.Empty;

    public string BannerBody { get; init; } = string.Empty;

    public bool ShowHistoricalBanner { get; init; }
}
