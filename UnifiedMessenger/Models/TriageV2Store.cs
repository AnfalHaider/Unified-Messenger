namespace UnifiedMessenger.Models;

/// <summary>
/// On-disk schema for <c>triage_v2.json</c>: triage items, operational threads, and kanban display order.
/// </summary>
public sealed class TriageV2Store
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public UnifiedMessengerStoreMetadata? Metadata { get; set; }

    public List<MessageTriageItemRecord> TriageItems { get; set; } = [];

    public List<ThreadData> Threads { get; set; } = [];

    public List<ThreadDisplayOrderEntry> DisplayOrder { get; set; } = [];
}

/// <summary>
/// JSON-serializable triage item with setters for round-trip persistence.
/// </summary>
public sealed class MessageTriageItemRecord
{
    public string Id { get; set; } = string.Empty;

    public string InstanceId { get; set; } = string.Empty;

    public string InstanceDisplayName { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string MessagePreview { get; set; } = string.Empty;

    public string MessageFullText { get; set; } = string.Empty;

    public string CustomerName { get; set; } = "Customer";

    public int UrgencyScore { get; set; }

    public MessageSentiment Sentiment { get; set; }

    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    public TriageInferenceSource InferenceSource { get; set; } = TriageInferenceSource.Heuristic;

    public CustomerIntent CustomerIntent { get; set; } = CustomerIntent.Inquiry;

    public string CoreSummary { get; set; } = string.Empty;

    public string ThreadId { get; set; } = string.Empty;

    public string ConversationKey { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public int OperationalUrgency { get; set; } = 1;

    public string AiIntentCategory { get; set; } = UnifiedMessengerIntentCategory.Inquiry;

    public string ClientSentiment { get; set; } = ClientSentimentLabel.Neutral;

    public string NextActionSummary { get; set; } = string.Empty;

    public string SuggestedAction { get; set; } = string.Empty;

    public bool IsSpamOrPromo { get; set; }

    public double EstimatedValue { get; set; }

    public bool IsRevenueLeakageRisk { get; set; }

    public InboundMessageKind MessageKind { get; set; } = InboundMessageKind.Text;

    public static MessageTriageItemRecord FromItem(MessageTriageItem item) =>
        new()
        {
            Id = item.Id,
            InstanceId = item.InstanceId,
            InstanceDisplayName = item.InstanceDisplayName,
            Platform = item.Platform,
            MessagePreview = item.MessagePreview,
            MessageFullText = item.MessageFullText,
            CustomerName = item.CustomerName,
            UrgencyScore = item.UrgencyScore,
            Sentiment = item.Sentiment,
            TimestampUtc = item.TimestampUtc,
            InferenceSource = item.InferenceSource,
            CustomerIntent = item.CustomerIntent,
            CoreSummary = item.CoreSummary,
            ThreadId = item.ThreadId,
            ConversationKey = item.ConversationKey,
            BranchName = item.BranchName,
            OperationalUrgency = item.OperationalUrgency,
            AiIntentCategory = item.AiIntentCategory,
            ClientSentiment = item.ClientSentiment,
            NextActionSummary = item.NextActionSummary,
            SuggestedAction = item.SuggestedAction,
            IsSpamOrPromo = item.IsSpamOrPromo,
            EstimatedValue = item.EstimatedValue,
            IsRevenueLeakageRisk = item.IsRevenueLeakageRisk,
            MessageKind = item.MessageKind
        };

    public MessageTriageItem ToItem() =>
        new()
        {
            Id = Id,
            InstanceId = InstanceId,
            InstanceDisplayName = InstanceDisplayName,
            Platform = Platform,
            MessagePreview = MessagePreview,
            MessageFullText = MessageFullText,
            CustomerName = CustomerName,
            UrgencyScore = UrgencyScore,
            Sentiment = Sentiment,
            TimestampUtc = TimestampUtc,
            InferenceSource = InferenceSource,
            CustomerIntent = CustomerIntent,
            CoreSummary = CoreSummary,
            ThreadId = ThreadId,
            ConversationKey = ConversationKey,
            BranchName = BranchName,
            OperationalUrgency = OperationalUrgency,
            AiIntentCategory = AiIntentCategory,
            ClientSentiment = ClientSentiment,
            NextActionSummary = NextActionSummary,
            SuggestedAction = SuggestedAction,
            IsSpamOrPromo = IsSpamOrPromo,
            EstimatedValue = EstimatedValue,
            IsRevenueLeakageRisk = IsRevenueLeakageRisk,
            MessageKind = MessageKind
        };
}
