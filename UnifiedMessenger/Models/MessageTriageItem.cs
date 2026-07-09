namespace UnifiedMessenger.Models;

public sealed class MessageTriageItem
{
    public required string Id { get; init; }

    public required string InstanceId { get; init; }

    public required string InstanceDisplayName { get; init; }

    public required string Platform { get; init; }

    public required string MessagePreview { get; init; }

    public string MessageFullText { get; init; } = string.Empty;

    public string CustomerName { get; init; } = "Customer";

    public int UrgencyScore { get; init; }

    public MessageSentiment Sentiment { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public TriageInferenceSource InferenceSource { get; init; } = TriageInferenceSource.Heuristic;

    public CustomerIntent CustomerIntent { get; init; } = CustomerIntent.Inquiry;

    public string CoreSummary { get; init; } = string.Empty;

    public string ThreadId { get; init; } = string.Empty;

    public string ConversationKey { get; init; } = string.Empty;

    public string BranchName { get; init; } = string.Empty;

    public int OperationalUrgency { get; init; } = 1;

    public string AiIntentCategory { get; init; } = UnifiedMessengerIntentCategory.Inquiry;

    public string ClientSentiment { get; init; } = ClientSentimentLabel.Neutral;

    public string NextActionSummary { get; init; } = string.Empty;

    public string SuggestedAction { get; init; } = string.Empty;

    public bool IsSpamOrPromo { get; init; }

    public double EstimatedValue { get; init; }

    public bool IsRevenueLeakageRisk { get; init; }

    public InboundMessageKind MessageKind { get; init; } = InboundMessageKind.Text;

    /// <summary>True when reconstructed from historical backfill rather than observed live (excluded from SLA).</summary>
    public bool IsBackfilled { get; init; }

    public string UrgencyLabel => UrgencyScore switch
    {
        >= 80 => "Critical",
        >= 55 => "High",
        >= 30 => "Medium",
        _ => "Low"
    };
}

public sealed class MessageTriageDashboardSnapshot
{
    public static MessageTriageDashboardSnapshot Empty { get; } = new();

    public int PositiveCount { get; init; }

    public int NeutralCount { get; init; }

    public int NegativeCount { get; init; }

    public IReadOnlyList<MessageTriageItem> UrgentQueue { get; init; } = [];

    public IReadOnlyList<MessageTriageItem> RecentInbound { get; init; } = [];

    public int TotalTriageCount => PositiveCount + NeutralCount + NegativeCount;

    public IReadOnlyList<DailySentimentPoint> WeeklySentiment { get; init; } = [];
}

public sealed class DailySentimentPoint
{
    public required string Label { get; init; }

    public int Positive { get; init; }

    public int Neutral { get; init; }

    public int Negative { get; init; }
}
