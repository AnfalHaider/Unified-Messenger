using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Ai;

public sealed class HeuristicTriageResult
{
    public required MessageTriageItem Item { get; init; }

    public required string ConversationKey { get; init; }

    public required string BranchName { get; init; }
}

public static class HeuristicTriageProcessor
{
    public static HeuristicTriageResult? Process(MessageTriageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messageText = ConversationNoiseFilter.CleanForInference(request.MessageText);
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return null;
        }

        var isSpam = ConversationNoiseFilter.IsPromoSpam(messageText);
        var urgency = isSpam
            ? 5
            : MessageTriageScorer.ScoreUrgency(messageText, request.ConversationHint);
        var sentiment = MessageTriageScorer.ClassifySentiment(messageText);
        var preview = messageText.Length <= 220
            ? messageText
            : messageText[..217] + "...";

        if (ConversationNoiseFilter.IsDomChromePollution(preview))
        {
            return null;
        }

        var branchName = BranchWorkspaceHelper.ResolveBranchKey(request.BranchKey, request.InstanceDisplayName);
        var conversationKey = ConversationKeyResolver.Resolve(
            request.Platform,
            request.ConversationKey,
            request.ConversationHint,
            request.CustomerName,
            messageText);
        var threadId = ConversationKeyResolver.BuildThreadId(request.InstanceId, conversationKey);
        var operationalUrgency = isSpam
            ? 1
            : ThreadRegistryService.MapOperationalUrgency(urgency);
        var intent = isSpam ? CustomerIntent.Spam : InferCustomerIntent(messageText);
        var aiIntent = MapIntent(intent);
        var nextAction = BuildHeuristicNextAction(isSpam, intent, urgency);
        var coreSummary = BuildHeuristicCoreSummary(preview, intent, isSpam);

        var item = new MessageTriageItem
        {
            Id = $"{request.InstanceId}|{Guid.NewGuid():N}",
            InstanceId = request.InstanceId,
            InstanceDisplayName = request.InstanceDisplayName,
            Platform = request.Platform,
            MessagePreview = preview,
            MessageFullText = messageText,
            CustomerName = request.CustomerName,
            UrgencyScore = urgency,
            Sentiment = sentiment,
            TimestampUtc = request.TimestampUtc,
            InferenceSource = TriageInferenceSource.Heuristic,
            ThreadId = threadId,
            ConversationKey = conversationKey,
            BranchName = branchName,
            OperationalUrgency = operationalUrgency,
            AiIntentCategory = aiIntent,
            ClientSentiment = sentiment == MessageSentiment.Negative
                ? ClientSentimentLabel.Frustrated
                : sentiment == MessageSentiment.Positive
                    ? ClientSentimentLabel.Positive
                    : ClientSentimentLabel.Neutral,
            CoreSummary = coreSummary,
            NextActionSummary = nextAction,
            SuggestedAction = isSpam ? "Ignore" : SuggestHeuristicAction(intent, urgency),
            IsSpamOrPromo = isSpam,
            CustomerIntent = intent,
            MessageKind = request.MessageKind,
            IsBackfilled = request.IsBackfilled
        };

        return new HeuristicTriageResult
        {
            Item = item,
            ConversationKey = conversationKey,
            BranchName = branchName
        };
    }

    internal static CustomerIntent InferCustomerIntent(string messageText)
    {
        var normalized = messageText.ToLowerInvariant();

        if (ContainsAny(normalized, "cancel", "refund", "complaint", "angry", "terrible", "unsatisfied"))
        {
            return CustomerIntent.Complaint;
        }

        if (ContainsAny(normalized, "book", "appointment", "schedule", "reschedule"))
        {
            return CustomerIntent.Booking;
        }

        if (ContainsAny(normalized, "price", "quote", "cost", "how much"))
        {
            return CustomerIntent.Inquiry;
        }

        return CustomerIntent.Inquiry;
    }

    internal static string BuildHeuristicCoreSummary(string preview, CustomerIntent intent, bool isSpam)
    {
        if (isSpam)
        {
            return "Promotional or automated message";
        }

        return intent switch
        {
            CustomerIntent.Booking => $"Booking request: {preview}",
            CustomerIntent.Complaint => $"Customer concern: {preview}",
            _ => preview
        };
    }

    internal static string BuildHeuristicNextAction(bool isSpam, CustomerIntent intent, int urgencyScore)
    {
        if (isSpam)
        {
            return "Promotional message — no action required";
        }

        return intent switch
        {
            CustomerIntent.Booking when urgencyScore >= 55 => "Confirm or reschedule the booking request",
            CustomerIntent.Booking => "Review booking details and reply",
            CustomerIntent.Complaint when urgencyScore >= 55 => "Escalate and respond to the complaint promptly",
            CustomerIntent.Complaint => "Acknowledge concern and propose next steps",
            _ when urgencyScore >= 80 => "Reply immediately to urgent customer message",
            _ => "Review message and send a helpful reply"
        };
    }

    internal static string SuggestHeuristicAction(CustomerIntent intent, int urgencyScore)
    {
        if (intent == CustomerIntent.Complaint && urgencyScore >= 55)
        {
            return "Escalate";
        }

        return "Reply";
    }

    private static string MapIntent(CustomerIntent intent) =>
        intent switch
        {
            CustomerIntent.Booking => UnifiedMessengerIntentCategory.Booking,
            CustomerIntent.Complaint => UnifiedMessengerIntentCategory.Complaint,
            CustomerIntent.Spam => UnifiedMessengerIntentCategory.Spam,
            _ => UnifiedMessengerIntentCategory.Inquiry
        };

    private static bool ContainsAny(string text, params string[] terms)
    {
        foreach (var term in terms)
        {
            if (text.Contains(term, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class MessageTriageRequest
{
    public required string InstanceId { get; init; }

    public required string InstanceDisplayName { get; init; }

    public string BranchKey { get; init; } = string.Empty;

    public required string Platform { get; init; }

    public required string MessageText { get; init; }

    public string CustomerName { get; init; } = "Customer";

    public string ConversationKey { get; init; } = string.Empty;

    public string ConversationHint { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public InboundMessageKind MessageKind { get; init; } = InboundMessageKind.Text;

    public bool AllowLlmInference { get; init; } = true;

    /// <summary>True when this message arrived via historical backfill rather than a live observation.</summary>
    public bool IsBackfilled { get; init; }
}
