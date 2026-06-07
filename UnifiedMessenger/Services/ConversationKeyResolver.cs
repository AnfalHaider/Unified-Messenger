
namespace UnifiedMessenger.Services;

/// <summary>
/// Canonical conversation identity for thread registry, resolve events, and insight dedupe.
/// Keys must match between JS ingress (<c>conversation-key.js</c>) and C# upsert/resolve paths.
/// </summary>
/// <remarks>
/// Platform conventions:
/// <list type="bullet">
/// <item>WhatsApp / WhatsApp Business — chat JID (e.g. <c>1234567890@c.us</c>)</item>
/// <item>Google Business — <c>review:{reviewId}</c></item>
/// <item>Meta Business — normalized conversation header title (same as thread-status-auditor)</item>
/// <item>Fallback — trimmed customer name, then message preview prefix</item>
/// </list>
/// </remarks>
public static class ConversationKeyResolver
{
    public const string ReviewKeyPrefix = "review:";

    public static string Resolve(
        string platform,
        string? conversationKey = null,
        string? conversationHint = null,
        string? customerName = null,
        string? messagePreview = null)
    {
        if (!string.IsNullOrWhiteSpace(conversationKey))
        {
            var explicitKey = NormalizeExplicitKey(conversationKey);
            if (!string.IsNullOrWhiteSpace(explicitKey))
            {
                return explicitKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(conversationHint))
        {
            var hintKey = NormalizeHintOrFallback(platform, conversationHint.Trim());
            if (!string.IsNullOrWhiteSpace(hintKey))
            {
                return hintKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            return CollapseWhitespace(customerName);
        }

        if (!string.IsNullOrWhiteSpace(messagePreview))
        {
            var preview = messagePreview.Trim();
            return preview.Length <= 48 ? preview : preview[..48].Trim();
        }

        return "unknown";
    }

    public static string BuildThreadId(string instanceId, string conversationKey)
    {
        var key = NormalizeExplicitKey(conversationKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            key = CollapseWhitespace(conversationKey);
        }

        return $"{instanceId.Trim()}|{key}";
    }

    public static string BuildReviewKey(string reviewId) =>
        string.IsNullOrWhiteSpace(reviewId)
            ? string.Empty
            : ReviewKeyPrefix + reviewId.Trim();

    public static bool IsReviewKey(string? conversationKey) =>
        !string.IsNullOrWhiteSpace(conversationKey) &&
        conversationKey.StartsWith(ReviewKeyPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsWhatsAppJid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        return value.EndsWith("@c.us", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeExplicitKey(string conversationKey)
    {
        var trimmed = CollapseWhitespace(conversationKey);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (trimmed.StartsWith(ReviewKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ReviewKeyPrefix + trimmed[ReviewKeyPrefix.Length..].Trim();
        }

        if (IsWhatsAppJid(trimmed))
        {
            return trimmed;
        }

        return trimmed;
    }

    private static string NormalizeHintOrFallback(string platform, string conversationHint)
    {
        if (conversationHint.StartsWith(ReviewKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeExplicitKey(conversationHint);
        }

        if (IsWhatsAppJid(conversationHint))
        {
            return conversationHint;
        }

        return CollapseWhitespace(conversationHint);
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
