using System.Text.Json.Serialization;

namespace UnifiedMessenger.Models;

/// <summary>JSON contract returned by the local triage model (camelCase or PascalCase keys).</summary>
public sealed class RichTriageLlmResponse
{
    [JsonPropertyName("UrgencyScore")]
    public int LegacyUrgencyScore { get; init; }

    [JsonPropertyName("Sentiment")]
    public string Sentiment { get; init; } = "Neutral";

    [JsonPropertyName("CustomerIntent")]
    public string CustomerIntent { get; init; } = "Inquiry";

    [JsonPropertyName("ExtractedEntities")]
    public RichTriageExtractedEntities ExtractedEntities { get; init; } = new();

    [JsonPropertyName("CoreSummary")]
    public string CoreSummary { get; init; } = string.Empty;

    [JsonPropertyName("AiIntentCategory")]
    public string AiIntentCategory { get; init; } = UnifiedMessengerIntentCategory.Inquiry;

    [JsonPropertyName("ClientSentiment")]
    public string ClientSentiment { get; init; } = ClientSentimentLabel.Neutral;

    [JsonPropertyName("OperationalUrgency")]
    public int LegacyOperationalUrgency { get; init; }

    [JsonPropertyName("urgencyScore")]
    public int OperationalUrgency { get; init; }

    [JsonPropertyName("EstimatedValue")]
    public double EstimatedValue { get; init; }

    [JsonPropertyName("NextActionSummary")]
    public string NextActionSummary { get; init; } = string.Empty;

    [JsonPropertyName("actionableSummary")]
    public string ActionableSummary { get; init; } = string.Empty;

    [JsonPropertyName("IsRevenueLeakageRisk")]
    public bool IsRevenueLeakageRisk { get; init; }

    [JsonPropertyName("isSpamOrPromo")]
    public bool IsSpamOrPromo { get; init; }

    [JsonPropertyName("intentCategory")]
    public string IntentCategory { get; init; } = string.Empty;

    [JsonPropertyName("suggestedAction")]
    public string SuggestedAction { get; init; } = string.Empty;
}
