using System.Text.RegularExpressions;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Deterministic enrichment layered on top of local LLM triage output.
/// Provides fail-safe fallbacks when inference is unavailable or partial.
/// </summary>
public static partial class UnifiedMessengerInsightsAnalyzer
{
    private static readonly string[] CommercialKeywords =
    [
        "price", "rate", "cost", "quote", "package", "pkr", "rs.", "rs ",
        "bridal", "makeup", "booking", "slot", "appointment", "available"
    ];

    private static readonly string[] OutboundOfferKeywords =
    [
        "we offer", "our rate", "package is", "total would be", "slot available",
        "can book", "confirm your", "quoted", "price is", "charges are"
    ];

    private static readonly string[] CriticalKeywords =
    [
        "refund", "lawyer", "sue", "worst", "never again", "scam", "fraud",
        "chargeback", "disgusting", "unacceptable"
    ];

    public static RichTriageLlmResponse Enrich(
        RichTriageLlmResponse response,
        RichTriageInferenceJob job,
        string? conversationTranscript = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(job);

        var combinedText = BuildAnalysisCorpus(job.MessageText, conversationTranscript);
        var intent = NormalizeIntent(
            response.AiIntentCategory,
            response.CustomerIntent,
            combinedText);
        var sentiment = NormalizeClientSentiment(
            response.ClientSentiment,
            response.Sentiment,
            combinedText);
        var operationalUrgency = response.OperationalUrgency > 0
            ? Math.Clamp(response.OperationalUrgency, 1, 5)
            : response.LegacyOperationalUrgency > 0
                ? Math.Clamp(response.LegacyOperationalUrgency, 1, 5)
                : MessageTriageInferenceRunner.MapOperationalUrgency(response.LegacyUrgencyScore);
        var estimatedValue = response.EstimatedValue > 0
            ? response.EstimatedValue
            : EstimateValue(combinedText, intent);
        var nextAction = string.IsNullOrWhiteSpace(response.NextActionSummary)
            ? BuildHeuristicNextAction(intent, sentiment, job.CustomerName, combinedText)
            : response.NextActionSummary.Trim();
        var isSpam = response.IsSpamOrPromo ||
                     ConversationNoiseFilter.IsPromoSpam(combinedText) ||
                     intent.Equals(UnifiedMessengerIntentCategory.Spam, StringComparison.OrdinalIgnoreCase);
        var hangingLead = !isSpam &&
                          (response.IsRevenueLeakageRisk ||
                           DetectHangingLead(combinedText, conversationTranscript));

        return new RichTriageLlmResponse
        {
            LegacyUrgencyScore = Math.Clamp(response.LegacyUrgencyScore, 0, 100),
            Sentiment = response.Sentiment,
            CustomerIntent = isSpam ? "Spam" : response.CustomerIntent,
            ExtractedEntities = response.ExtractedEntities,
            CoreSummary = response.CoreSummary,
            AiIntentCategory = isSpam ? UnifiedMessengerIntentCategory.Spam : intent,
            ClientSentiment = sentiment,
            OperationalUrgency = operationalUrgency,
            EstimatedValue = isSpam ? 0 : estimatedValue,
            NextActionSummary = isSpam ? "Promotional message — no action required" : nextAction,
            ActionableSummary = isSpam ? "Promotional message — no action required" : nextAction,
            IsRevenueLeakageRisk = hangingLead,
            IsSpamOrPromo = isSpam,
            IntentCategory = isSpam ? UnifiedMessengerIntentCategory.Spam : response.IntentCategory,
            SuggestedAction = isSpam ? "Ignore" : response.SuggestedAction
        };
    }

    public static MessageTriageItem ApplyOperationalInsights(
        MessageTriageItem item,
        double latencyMinutes = 0)
    {
        ArgumentNullException.ThrowIfNull(item);

        var corpus = item.MessagePreview;
        if (item.IsSpamOrPromo)
        {
            return item;
        }

        var intent = NormalizeIntent(item.AiIntentCategory, item.CustomerIntent.ToString(), corpus);
        var sentiment = NormalizeClientSentiment(
            item.ClientSentiment,
            item.Sentiment.ToString(),
            corpus);
        var estimatedValue = item.EstimatedValue > 0 ? item.EstimatedValue : EstimateValue(corpus, intent);
        var nextAction = string.IsNullOrWhiteSpace(item.NextActionSummary)
            ? BuildHeuristicNextAction(intent, sentiment, item.CustomerName, corpus)
            : item.NextActionSummary;
        var leakage = item.IsRevenueLeakageRisk ||
                      ResolveRevenueLeakageRisk(intent, latencyMinutes, corpus);

        return new MessageTriageItem
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
            ExtractedEntities = item.ExtractedEntities,
            ThreadId = item.ThreadId,
            ConversationKey = item.ConversationKey,
            BranchName = item.BranchName,
            OperationalUrgency = BoostUrgencyForCriticalSentiment(item.OperationalUrgency, sentiment),
            AiIntentCategory = intent,
            ClientSentiment = sentiment,
            NextActionSummary = nextAction,
            SuggestedAction = item.SuggestedAction,
            IsSpamOrPromo = item.IsSpamOrPromo,
            EstimatedValue = estimatedValue,
            IsRevenueLeakageRisk = leakage
        };
    }

    internal static bool ResolveRevenueLeakageRisk(
        string intent,
        double latencyMinutes,
        string? corpus)
    {
        if (DetectHangingLead(corpus ?? string.Empty, null))
        {
            return true;
        }

        var isCommercial =
            intent.Equals(UnifiedMessengerIntentCategory.PriceInquiry, StringComparison.OrdinalIgnoreCase) ||
            intent.Equals(UnifiedMessengerIntentCategory.Booking, StringComparison.OrdinalIgnoreCase) ||
            intent.Equals(UnifiedMessengerIntentCategory.Lead, StringComparison.OrdinalIgnoreCase);

        return isCommercial && latencyMinutes >= OperationalThresholds.GetRevenueLeakageMinutes();
    }

    internal static bool DetectHangingLead(string corpus, string? transcript)
    {
        var text = string.IsNullOrWhiteSpace(transcript) ? corpus : transcript;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.ToLowerInvariant();
        var hasOutboundOffer = OutboundOfferKeywords.Any(normalized.Contains);
        if (!hasOutboundOffer)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return hasOutboundOffer && CommercialKeywords.Any(normalized.Contains);
        }

        var lastIncomingIndex = normalized.LastIndexOf("[incoming]", StringComparison.Ordinal);
        var lastOutgoingIndex = normalized.LastIndexOf("[outgoing]", StringComparison.Ordinal);
        return lastOutgoingIndex > lastIncomingIndex && hasOutboundOffer;
    }

    internal static double EstimateValue(string corpus, string intent)
    {
        if (string.IsNullOrWhiteSpace(corpus))
        {
            return 0;
        }

        var match = PkrAmountPattern().Match(corpus);
        if (match.Success && double.TryParse(match.Groups["amount"].Value.Replace(",", ""), out var explicitValue))
        {
            return explicitValue;
        }

        var normalized = corpus.ToLowerInvariant();
        if (normalized.Contains("bridal", StringComparison.Ordinal) ||
            normalized.Contains("wedding", StringComparison.Ordinal))
        {
            return 85000;
        }

        if (normalized.Contains("party makeup", StringComparison.Ordinal) ||
            normalized.Contains("bridal makeup", StringComparison.Ordinal))
        {
            return 45000;
        }

        return intent switch
        {
            _ when intent.Equals(UnifiedMessengerIntentCategory.Booking, StringComparison.OrdinalIgnoreCase) => 25000,
            _ when intent.Equals(UnifiedMessengerIntentCategory.PriceInquiry, StringComparison.OrdinalIgnoreCase) => 15000,
            _ when intent.Equals(UnifiedMessengerIntentCategory.Lead, StringComparison.OrdinalIgnoreCase) => 12000,
            _ => 0
        };
    }

    internal static string BuildHeuristicNextAction(
        string intent,
        string sentiment,
        string customerName,
        string corpus)
    {
        var name = string.IsNullOrWhiteSpace(customerName) ? "Client" : customerName.Trim();
        if (sentiment.Equals(ClientSentimentLabel.Critical, StringComparison.OrdinalIgnoreCase))
        {
            return $"{name} is escalated — call immediately and confirm resolution steps.";
        }

        return intent switch
        {
            _ when intent.Equals(UnifiedMessengerIntentCategory.Complaint, StringComparison.OrdinalIgnoreCase) =>
                $"{name} raised a complaint — acknowledge, apologize, and propose a fix within 15 minutes.",
            _ when intent.Equals(UnifiedMessengerIntentCategory.Booking, StringComparison.OrdinalIgnoreCase) =>
                $"{name} wants to book — confirm date, time, branch slot, and send a hold message.",
            _ when intent.Equals(UnifiedMessengerIntentCategory.PriceInquiry, StringComparison.OrdinalIgnoreCase) =>
                $"{name} asked for pricing — send package options with PKR totals and upsell one premium tier.",
            _ when intent.Equals(UnifiedMessengerIntentCategory.Lead, StringComparison.OrdinalIgnoreCase) =>
                $"{name} is a new lead — qualify service need and offer a quick consultation slot.",
            _ => $"{name} sent a new inquiry — reply with branch availability and ask one clarifying question."
        };
    }

    internal static string BuildAnalysisCorpus(string messageText, string? conversationTranscript)
    {
        var message = ConversationNoiseFilter.Strip(messageText);
        if (string.IsNullOrWhiteSpace(conversationTranscript))
        {
            return message;
        }

        return $"{conversationTranscript}\n{message}".Trim();
    }

    private static string NormalizeIntent(string? aiIntent, string? customerIntent, string corpus)
    {
        if (!string.IsNullOrWhiteSpace(aiIntent) &&
            !aiIntent.Equals(UnifiedMessengerIntentCategory.Inquiry, StringComparison.OrdinalIgnoreCase))
        {
            return aiIntent.Trim();
        }

        var normalized = corpus.ToLowerInvariant();
        if (normalized.Contains("complaint", StringComparison.Ordinal) ||
            normalized.Contains("refund", StringComparison.Ordinal) ||
            normalized.Contains("worst service", StringComparison.Ordinal))
        {
            return UnifiedMessengerIntentCategory.Complaint;
        }

        if (normalized.Contains("book", StringComparison.Ordinal) ||
            normalized.Contains("appointment", StringComparison.Ordinal) ||
            normalized.Contains("slot", StringComparison.Ordinal))
        {
            return UnifiedMessengerIntentCategory.Booking;
        }

        if (normalized.Contains("price", StringComparison.Ordinal) ||
            normalized.Contains("rate", StringComparison.Ordinal) ||
            normalized.Contains("how much", StringComparison.Ordinal) ||
            normalized.Contains("package", StringComparison.Ordinal))
        {
            return UnifiedMessengerIntentCategory.PriceInquiry;
        }

        if (customerIntent?.Contains("Booking", StringComparison.OrdinalIgnoreCase) == true)
        {
            return UnifiedMessengerIntentCategory.Booking;
        }

        if (customerIntent?.Contains("Complaint", StringComparison.OrdinalIgnoreCase) == true)
        {
            return UnifiedMessengerIntentCategory.Complaint;
        }

        return UnifiedMessengerIntentCategory.Inquiry;
    }

    private static string NormalizeClientSentiment(
        string? clientSentiment,
        string? legacySentiment,
        string corpus)
    {
        if (!string.IsNullOrWhiteSpace(clientSentiment) &&
            !clientSentiment.Equals(ClientSentimentLabel.Neutral, StringComparison.OrdinalIgnoreCase))
        {
            return clientSentiment.Trim();
        }

        var normalized = corpus.ToLowerInvariant();
        if (CriticalKeywords.Any(normalized.Contains))
        {
            return ClientSentimentLabel.Critical;
        }

        if (normalized.Contains("angry", StringComparison.Ordinal) ||
            normalized.Contains("frustrated", StringComparison.Ordinal) ||
            normalized.Contains("disappointed", StringComparison.Ordinal) ||
            legacySentiment?.Equals("Negative", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ClientSentimentLabel.Frustrated;
        }

        if (normalized.Contains("thank", StringComparison.Ordinal) ||
            legacySentiment?.Equals("Positive", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ClientSentimentLabel.Positive;
        }

        return ClientSentimentLabel.Neutral;
    }

    private static int BoostUrgencyForCriticalSentiment(int urgency, string sentiment)
    {
        if (sentiment.Equals(ClientSentimentLabel.Critical, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(urgency, 5);
        }

        if (sentiment.Equals(ClientSentimentLabel.Frustrated, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(urgency, 4);
        }

        return Math.Clamp(urgency, 1, 5);
    }

    [GeneratedRegex(@"(?:pkr|rs\.?|₨)\s*(?<amount>\d[\d,]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PkrAmountPattern();
}
