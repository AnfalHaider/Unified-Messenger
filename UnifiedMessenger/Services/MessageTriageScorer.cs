using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class MessageTriageScorer
{
    private static readonly string[] CriticalKeywords =
    [
        "urgent", "emergency", "cancel", "cancellation", "refund", "complaint",
        "angry", "lawsuit", "unsatisfied", "terrible", "worst", "immediately",
        "asap", "overcharged", "no show", "reschedule today"
    ];

    private static readonly string[] HighKeywords =
    [
        "booking", "appointment", "schedule", "reschedule", "available",
        "price", "quote", "order", "delivery", "invoice", "payment",
        "waiting", "follow up", "follow-up", "confirm"
    ];

    private static readonly string[] PositiveKeywords =
    [
        "thank", "thanks", "great", "excellent", "love", "amazing",
        "happy", "wonderful", "perfect", "appreciate", "helpful"
    ];

    private static readonly string[] NegativeKeywords =
    [
        "bad", "poor", "late", "delay", "broken", "damaged", "rude",
        "never", "disappointed", "unhappy", "issue", "problem", "wrong"
    ];

    public static int ScoreUrgency(string messageText, string? conversationHint = null)
    {
        var combined = $"{messageText} {conversationHint}".ToLowerInvariant();
        var score = 10;

        foreach (var keyword in CriticalKeywords)
        {
            if (combined.Contains(keyword, StringComparison.Ordinal))
            {
                score += 35;
            }
        }

        foreach (var keyword in HighKeywords)
        {
            if (combined.Contains(keyword, StringComparison.Ordinal))
            {
                score += 18;
            }
        }

        if (messageText.Length >= 180)
        {
            score += 8;
        }

        if (messageText.Contains('?', StringComparison.Ordinal))
        {
            score += 6;
        }

        return Math.Clamp(score, 0, 100);
    }

    public static MessageSentiment ClassifySentiment(string messageText)
    {
        var normalized = messageText.ToLowerInvariant();
        var positive = CountMatches(normalized, PositiveKeywords);
        var negative = CountMatches(normalized, NegativeKeywords);

        if (negative > positive && negative > 0)
        {
            return MessageSentiment.Negative;
        }

        if (positive > negative && positive > 0)
        {
            return MessageSentiment.Positive;
        }

        return MessageSentiment.Neutral;
    }

    private static int CountMatches(string text, IEnumerable<string> keywords)
    {
        var count = 0;
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
