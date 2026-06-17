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

    /// <summary>Conversations returned by the IndexedDB-direct history read (diagnostic).</summary>
    public int DbConversationsFound { get; init; }

    /// <summary>Conversations whose last message was from us → marked answered (diagnostic).</summary>
    public int AnsweredReconciled { get; init; }

    /// <summary>Legacy title-keyed threads migrated to a stable JID (diagnostic).</summary>
    public int KeysMigrated { get; init; }

    /// <summary>Compact stage trace from the IndexedDB read (which DBs/stores it saw, records scanned).</summary>
    public string? DbDiagnostic { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsScrapeOnly { get; init; }

    public string? ScrapeOnlyReason { get; init; }

    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);
}
