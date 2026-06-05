using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Unified snapshot for the merged Operations Command Center dashboard (Phase 1 facade).
/// </summary>
public sealed class OperationsCommandCenterSnapshot
{
    public static OperationsCommandCenterSnapshot Empty { get; } = new();

    public const string DashboardTitle = "Operations Command Center";

    public string ScopeLabel { get; init; } = "Showing: All Branches";

    public string? BranchInstanceId { get; init; }

    public IReadOnlyList<MessengerInstance> FilteredInstances { get; init; } = [];

    public UnifiedMessengerDashboardSnapshot ThreadOperations { get; init; } =
        UnifiedMessengerDashboardSnapshot.Empty;

    public OperationsStatusSnapshot Status { get; init; } = OperationsStatusSnapshot.Empty;

    public OperationsPlatformIntelligenceSnapshot PlatformIntelligence { get; init; } =
        OperationsPlatformIntelligenceSnapshot.Empty;

    public OperationsAnalyticsTrendSnapshot AnalyticsTrends { get; init; } =
        OperationsAnalyticsTrendSnapshot.Empty;

    public IReadOnlyList<OperationsInsightFeedItem> AiInsightFeed { get; init; } = [];

    public IReadOnlyList<DashboardInstanceHealthChip> InstanceHealthChips { get; init; } = [];
}

public sealed class OperationsStatusSnapshot
{
    public static OperationsStatusSnapshot Empty { get; } = new();

    public int OpenThreadCount { get; init; }

    public int HangingLeadCount { get; init; }

    public int ImmediateActionCount { get; init; }

    public double TotalRevenueAtRisk { get; init; }

    public string AverageReplyTime { get; init; } = "—";

    public string AverageReplyTimeSubtext { get; init; } = string.Empty;

    public string SlaBreaches { get; init; } = "—";

    public int SlaBreachesNumeric { get; init; }

    public string SlaThresholdSubtext { get; init; } = string.Empty;

    public string ResponseRate { get; init; } = "—";

    public string PeakHour { get; init; } = "—";

    public string DailyTrend { get; init; } = "—";

    public int SentCount { get; init; }

    public int ReceivedCount { get; init; }

    public bool HasMessageVolume { get; init; }

    public bool HasReplyMetrics { get; init; }

    public IReadOnlyList<UnifiedMessengerPlatformHealthIndicator> PlatformHealth { get; init; } = [];
}

public sealed class OperationsPlatformIntelligenceSnapshot
{
    public static OperationsPlatformIntelligenceSnapshot Empty { get; } = new();

    public CustomerTrustSnapshot CustomerTrust { get; init; } = new();

    public CustomerTrustDisplay CustomerTrustDisplay { get; init; } = new()
    {
        AggregateRating = "—",
        UnrepliedReviews = "—"
    };

    public MetaResponseEfficiencySnapshot MetaResponse { get; init; } = new();

    public MetaResponseDisplay MetaResponseDisplay { get; init; } = new()
    {
        AverageResponse = "—",
        EfficiencyRating = "Awaiting data",
        SampleCount = "—",
        LastInbound = "—",
        LastReply = "—"
    };

    public bool HasGoogleInstances { get; init; }

    public bool HasMetaInstances { get; init; }

    public IReadOnlyList<string> GoogleInstanceIds { get; init; } = [];

    public IReadOnlyList<string> MetaInstanceIds { get; init; } = [];
}

public sealed class OperationsAnalyticsTrendSnapshot
{
    public static OperationsAnalyticsTrendSnapshot Empty { get; } = new();

    public IReadOnlyList<DailyActivityPoint> WeeklyActivity { get; init; } = [];

    public MessageTriageDashboardSnapshot Triage { get; init; } = MessageTriageDashboardSnapshot.Empty;

    public IReadOnlyList<OperationalHighlightItem> Highlights { get; init; } = [];

    public int SentCount { get; init; }

    public int ReceivedCount { get; init; }

    public bool HasMessageVolume { get; init; }

    public bool HasReplyMetrics { get; init; }
}

public enum OperationsInsightFeedKind
{
    ThreadAction,
    ExecutiveInsight
}

public sealed class OperationsInsightFeedItem
{
    public OperationsInsightFeedKind Kind { get; init; }

    public required string DedupeKey { get; init; }

    public required string CustomerName { get; init; }

    public required string BranchName { get; init; }

    public required string Summary { get; init; }

    public string? InstanceId { get; init; }

    public string? ThreadId { get; init; }

    public string? TriageItemId { get; init; }

    public string IntentLabel { get; init; } = string.Empty;

    public string UrgencyLabel { get; init; } = string.Empty;

    public string SourceLabel { get; init; } = string.Empty;

    public int PriorityScore { get; init; }

    public ThreadData? Thread { get; init; }

    public ExecutiveInsightCardDisplay? ExecutiveCard { get; init; }
}
