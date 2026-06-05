namespace UnifiedMessenger.Models;

public sealed class RichTriageInferenceJob
{
    public required string TriageItemId { get; init; }

    public required string InstanceId { get; init; }

    public required string InstanceDisplayName { get; init; }

    public required string Platform { get; init; }

    public required string MessageText { get; init; }

    public string CustomerName { get; init; } = "Customer";

    public string ConversationHint { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public int HeuristicUrgencyScore { get; init; }

    public MessageSentiment HeuristicSentiment { get; init; }

    /// <summary>Recent thread lines from conversation-context-scraper.js, when available.</summary>
    public string ConversationTranscript { get; init; } = string.Empty;
}
