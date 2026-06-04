namespace UnifiedMessenger.Models;

public sealed class MessageTriageItem
{
    public required string Id { get; init; }

    public required string InstanceId { get; init; }

    public required string InstanceDisplayName { get; init; }

    public required string Platform { get; init; }

    public required string MessagePreview { get; init; }

    public string CustomerName { get; init; } = "Customer";

    public int UrgencyScore { get; init; }

    public MessageSentiment Sentiment { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public TriageInferenceSource InferenceSource { get; init; } = TriageInferenceSource.Heuristic;

    public CustomerIntent CustomerIntent { get; init; } = CustomerIntent.Inquiry;

    public string CoreSummary { get; init; } = string.Empty;

    public RichTriageExtractedEntities ExtractedEntities { get; init; } = new();

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

    public IReadOnlyList<DailySentimentPoint> WeeklySentiment { get; init; } = [];
}

public sealed class DailySentimentPoint
{
    public required string Label { get; init; }

    public int Positive { get; init; }

    public int Neutral { get; init; }

    public int Negative { get; init; }
}
