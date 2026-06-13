namespace UnifiedMessenger.Services.Backfill;

public sealed class BackfillResult
{
    public int TriageEnqueued { get; init; }

    public int TriageSkippedDuplicate { get; init; }

    public int AnalyticsInboundRecorded { get; init; }

    public int SlaCandidatesRecorded { get; init; }

    public int DailyAggregateDaysMerged { get; init; }

    public int SidebarRowsCaptured { get; init; }

    public int HistoryChunksProcessed { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsScrapeOnly { get; init; }

    public string? ScrapeOnlyReason { get; init; }

    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);
}
