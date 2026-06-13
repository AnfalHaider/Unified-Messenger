using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Pure presentation helpers for Operations Command Center thread cards.
/// </summary>
public static class OperationsThreadCardPresentationHelper
{
    public static string BuildFallbackSummary(ThreadData thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (thread.IsSpamOrPromo)
        {
            return "Promotional message — no action required";
        }

        if (!string.IsNullOrWhiteSpace(thread.SuggestedAction))
        {
            return $"Suggested: {thread.SuggestedAction.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(thread.LastMessagePreview))
        {
            return thread.LastMessagePreview.Trim();
        }

        var intent = UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(thread.AiIntentCategory);
        if (!intent.Equals("Inquiry", StringComparison.OrdinalIgnoreCase))
        {
            return $"{intent} — awaiting reply";
        }

        return "—";
    }
}
