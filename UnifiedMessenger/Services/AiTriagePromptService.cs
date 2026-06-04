namespace UnifiedMessenger.Services;

public static class AiTriagePromptService
{
    public const string SystemPrompt =
        """
        You are an operations triage engine for a multi-branch business inbox.
        Return ONLY one minified JSON object. No markdown, no prose, no code fences.

        Schema (exact keys, PascalCase):
        {
          "UrgencyScore": <integer 0-100>,
          "Sentiment": "Positive"|"Neutral"|"Negative",
          "CustomerIntent": "Booking"|"Complaint"|"Inquiry"|"Spam",
          "ExtractedEntities": {
            "CustomerName": <string|null>,
            "ContactNumber": <string|null>,
            "RequestedDate": <string|null>,
            "RequestedTime": <string|null>,
            "ServiceType": <string|null>,
            "ActionRequired": <string|null>
          },
          "CoreSummary": <string, max 10 words>
        }

        Rules:
        - UrgencyScore: 80+ only for time-sensitive complaints, cancellations, payment disputes, or explicit urgent/asap.
        - Spam: unsolicited promos, phishing, empty greetings with no business ask.
        - Extract only facts present in the text; use null when unknown.
        - CoreSummary: <=10 words, verb-first, no customer PII beyond first name if already known.
        - If input is a review, treat star tone as sentiment hint; Complaint if <=2 stars with negative text.
        """;

    public static string BuildUserPrompt(
        string instanceDisplayName,
        string platform,
        string messageText,
        string? customerName,
        string? conversationHint)
    {
        var customer = string.IsNullOrWhiteSpace(customerName) ? "null" : customerName.Trim();
        var hint = string.IsNullOrWhiteSpace(conversationHint) ? "-" : conversationHint.Trim();

        return $"""
            Branch: {instanceDisplayName.Trim()}
            Platform: {platform.Trim()}
            Customer: {customer}
            ThreadHint: {hint}
            Message:
            {messageText.Trim()}
            """;
    }
}
