
using UnifiedMessenger.Models;

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

    public const string MetaMessageKeyPrefix = "meta:msg:";

    public static string Resolve(
        string platform,
        string? conversationKey = null,
        string? conversationHint = null,
        string? customerName = null,
        string? messagePreview = null)
    {
        var normalizedPlatform = PlatformDefinition.NormalizePlatformId(platform);

        if (!string.IsNullOrWhiteSpace(conversationKey))
        {
            var explicitKey = NormalizeExplicitKey(conversationKey, normalizedPlatform);
            if (!string.IsNullOrWhiteSpace(explicitKey))
            {
                return explicitKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(conversationHint))
        {
            var hintKey = NormalizeHintOrFallback(normalizedPlatform, conversationHint.Trim());
            if (!string.IsNullOrWhiteSpace(hintKey))
            {
                return hintKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var nameKey = CollapseWhitespace(customerName);
            if (!IsGenericConversationLabel(nameKey, normalizedPlatform))
            {
                return nameKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(messagePreview))
        {
            var preview = messagePreview.Trim();
            if (normalizedPlatform.Equals("metabusiness", StringComparison.OrdinalIgnoreCase) &&
                preview.Length >= 8)
            {
                return BuildMetaMessageFingerprint(preview);
            }

            return preview.Length <= 48 ? preview : preview[..48].Trim();
        }

        return "unknown";
    }

    public static bool IsGenericConversationLabel(string? value, string? platform = null)
    {
        var trimmed = CollapseWhitespace(value ?? string.Empty);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        var normalized = trimmed.ToLowerInvariant();
        return normalized switch
        {
            "inbox" or "messages" or "message requests" or "messenger" or "meta business inbox"
                or "meta business suite" or "all messages" or "unread" or "archived" or "spam"
                or "business inbox" or "customer messages" => true,
            _ when normalized.StartsWith("meta business", StringComparison.Ordinal) => true,
            _ => false
        };
    }

    internal static string BuildMetaMessageFingerprint(string messagePreview)
    {
        var normalized = CollapseWhitespace(messagePreview);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "unknown";
        }

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return MetaMessageKeyPrefix + hash[..12];
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

    internal static string NormalizeExplicitKey(string conversationKey, string? platform = null)
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

        if (trimmed.StartsWith(MetaMessageKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return MetaMessageKeyPrefix + trimmed[MetaMessageKeyPrefix.Length..].Trim().ToLowerInvariant();
        }

        if (IsWhatsAppJid(trimmed))
        {
            return trimmed;
        }

        if (platform?.Equals("metabusiness", StringComparison.OrdinalIgnoreCase) == true &&
            IsGenericConversationLabel(trimmed, platform))
        {
            return string.Empty;
        }

        return trimmed;
    }

    private static string NormalizeHintOrFallback(string platform, string conversationHint)
    {
        if (conversationHint.StartsWith(ReviewKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeExplicitKey(conversationHint, platform);
        }

        if (conversationHint.StartsWith(MetaMessageKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeExplicitKey(conversationHint, platform);
        }

        if (IsWhatsAppJid(conversationHint))
        {
            return conversationHint;
        }

        var collapsed = CollapseWhitespace(conversationHint);
        if (platform.Equals("metabusiness", StringComparison.OrdinalIgnoreCase) &&
            IsGenericConversationLabel(collapsed, platform))
        {
            return string.Empty;
        }

        return collapsed;
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
