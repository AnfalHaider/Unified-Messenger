using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Pure presentation helpers for Operations Command Center thread cards.
/// </summary>
public static class OperationsThreadCardPresentationHelper
{
    public const string NoMessageCapturedPlaceholder = "(No message captured yet)";

    public static string BuildMessagePreview(ThreadData thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (thread.IsSpamOrPromo)
        {
            return "Promotional message — no action required";
        }

        if (!string.IsNullOrWhiteSpace(thread.LastMessagePreview))
        {
            return thread.LastMessagePreview.Trim();
        }

        var intent = UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(thread.AiIntentCategory);
        if (!intent.Equals("Inquiry", StringComparison.OrdinalIgnoreCase))
        {
            return $"{intent} thread — awaiting reply";
        }

        return NoMessageCapturedPlaceholder;
    }

    public static string BuildOpsHint(ThreadData thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (!string.IsNullOrWhiteSpace(thread.NextActionSummary))
        {
            return thread.NextActionSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(thread.SuggestedAction))
        {
            return $"Suggested: {thread.SuggestedAction.Trim()}";
        }

        return string.Empty;
    }

    public static string BuildRelativeTime(DateTimeOffset lastMessageTimeUtc)
    {
        var elapsed = DateTimeOffset.UtcNow - lastMessageTimeUtc.ToUniversalTime();
        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }

        if (elapsed.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)Math.Round(elapsed.TotalMinutes));
            return $"{minutes}m ago";
        }

        if (elapsed.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)Math.Round(elapsed.TotalHours));
            return $"{hours}h ago";
        }

        var days = Math.Max(1, (int)Math.Round(elapsed.TotalDays));
        return $"{days}d ago";
    }

    [Obsolete("Use BuildMessagePreview and BuildOpsHint instead.")]
    public static string BuildFallbackSummary(ThreadData thread) =>
        BuildMessagePreview(thread);
}
