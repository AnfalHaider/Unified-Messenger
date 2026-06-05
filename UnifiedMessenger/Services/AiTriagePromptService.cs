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
          "CoreSummary": <string, max 10 words>,
          "AiIntentCategory": "Booking"|"Complaint"|"Price_Inquiry"|"Lead"|"Inquiry",
          "ClientSentiment": "Positive"|"Neutral"|"Frustrated"|"Critical",
          "OperationalUrgency": <integer 1-5>,
          "EstimatedValue": <number, PKR revenue at risk if lead stalls>,
          "NextActionSummary": <string, one sentence manager directive>,
          "IsRevenueLeakageRisk": <boolean, true when quote/booking offered and customer silent >30m>
        }

        Rules:
        - UrgencyScore: 80+ only for time-sensitive complaints, cancellations, payment disputes, or explicit urgent/asap.
        - OperationalUrgency: map 1=low routine, 5=immediate manager action.
        - ClientSentiment: Frustrated for annoyed tone; Critical for threats, chargebacks, or repeated unanswered outreach.
        - AiIntentCategory: Price_Inquiry when asking rates/packages; Lead for new prospect; Booking for slot/date requests.
        - NextActionSummary: one actionable sentence for branch managers (no raw message dump).
        - IsRevenueLeakageRisk: true when business offered pricing/slot and customer has not replied (inferred from thread).
        - EstimatedValue: rough PKR value for bridal/premium services when pricing discussed; else 0.
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
        string? conversationHint,
        string? conversationTranscript = null)
    {
        var customer = string.IsNullOrWhiteSpace(customerName) ? "null" : customerName.Trim();
        var hint = string.IsNullOrWhiteSpace(conversationHint) ? "-" : conversationHint.Trim();
        var message = ConversationNoiseFilter.Strip(messageText);
        var transcript = string.IsNullOrWhiteSpace(conversationTranscript)
            ? string.Empty
            : conversationTranscript.Trim();

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return $"""
                Branch: {instanceDisplayName.Trim()}
                Platform: {platform.Trim()}
                Customer: {customer}
                ThreadHint: {hint}
                Message:
                {message}
                """;
        }

        return $"""
            Branch: {instanceDisplayName.Trim()}
            Platform: {platform.Trim()}
            Customer: {customer}
            ThreadHint: {hint}
            RecentThread:
            {transcript}
            LatestMessage:
            {message}
            """;
    }
}
