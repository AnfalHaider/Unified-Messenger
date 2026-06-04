namespace UnifiedMessenger.Models;

/// <summary>JSON contract returned by the local triage model (PascalCase keys).</summary>
public sealed class RichTriageLlmResponse
{
    public int UrgencyScore { get; init; }

    public string Sentiment { get; init; } = "Neutral";

    public string CustomerIntent { get; init; } = "Inquiry";

    public RichTriageExtractedEntities ExtractedEntities { get; init; } = new();

    public string CoreSummary { get; init; } = string.Empty;
}
