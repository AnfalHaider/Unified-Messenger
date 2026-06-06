namespace UnifiedMessenger.Services;

public static class AiTriagePromptService
{
    public const string SystemPrompt =
        """
        You are an operations triage engine for a multi-branch beauty salon inbox.
        Return ONLY one minified JSON object. No markdown, no prose, no code fences.

        Required schema (exact keys, camelCase):
        {
          "isSpamOrPromo": <boolean>,
          "intentCategory": "Booking"|"Complaint"|"Pricing"|"Spam"|"General",
          "urgencyScore": <integer 1-5>,
          "actionableSummary": <string, max 15 words, verb-first manager directive>,
          "suggestedAction": "Ignore"|"Reply with Pricing"|"Book Appointment"|"Call Client"|"Escalate"
        }

        Rules:
        - isSpamOrPromo: true for unsolicited B2B promos, bulk marketing, phishing, or empty greetings with no business ask.
        - intentCategory: Pricing when asking rates/packages; Booking for slot/date requests; Complaint for service issues; Spam when promo; General otherwise.
        - urgencyScore: 1=routine, 5=immediate manager action (urgent bridal booking, angry client, cancellation).
        - actionableSummary: NEVER repeat raw message text or timestamps. State what the manager must do.
        - suggestedAction: Ignore when isSpamOrPromo is true.
        - Bridal/urgent Saturday booking with pricing ask => urgencyScore 5, intentCategory Booking, suggestedAction "Reply with Pricing".
        """;

    public const string StrictJsonRetrySuffix =
        "\n\nIMPORTANT: Your previous reply was not valid JSON. Return ONLY the JSON object matching the schema above.";

    public static string BuildUserPrompt(
        string instanceDisplayName,
        string platform,
        string messageText,
        string? customerName,
        string? conversationHint,
        string? conversationTranscript = null,
        bool strictJsonRetry = false)
    {
        var customer = string.IsNullOrWhiteSpace(customerName) ? "null" : customerName.Trim();
        var hint = string.IsNullOrWhiteSpace(conversationHint) ? "-" : ConversationNoiseFilter.CleanForInference(conversationHint);
        var message = ConversationNoiseFilter.CleanForInference(messageText);
        var transcript = string.IsNullOrWhiteSpace(conversationTranscript)
            ? string.Empty
            : conversationTranscript.Trim();
        var retry = strictJsonRetry ? StrictJsonRetrySuffix : string.Empty;

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return $"""
                Branch: {instanceDisplayName.Trim()}
                Platform: {platform.Trim()}
                Customer: {customer}
                ThreadHint: {hint}
                Message:
                {message}{retry}
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
            {message}{retry}
            """;
    }
}
