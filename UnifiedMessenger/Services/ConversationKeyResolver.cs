
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
/// <item>Fallback — trimmed customer name, then message preview prefix</item>
/// </list>
/// </remarks>
public static class ConversationKeyResolver
{
    public static string Resolve(
        string platform,
        string? conversationKey = null,
        string? conversationHint = null,
        string? customerName = null,
        string? messagePreview = null)
    {
        _ = PlatformDefinition.NormalizePlatformId(platform);

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
            var hintKey = NormalizeHintOrFallback(conversationHint.Trim());
            if (!string.IsNullOrWhiteSpace(hintKey))
            {
                return hintKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var nameKey = CollapseWhitespace(customerName);
            if (!IsGenericConversationLabel(nameKey))
            {
                return nameKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(messagePreview))
        {
            var preview = messagePreview.Trim();
            return preview.Length <= 48 ? preview : preview[..48].Trim();
        }

        return "unknown";
    }

    public static bool IsGenericConversationLabel(string? value)
    {
        var trimmed = CollapseWhitespace(value ?? string.Empty);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        var normalized = trimmed.ToLowerInvariant();
        return normalized switch
        {
            "inbox" or "messages" or "message requests" or "all messages" or "unread" or "archived" or "spam"
                => true,
            _ => false
        };
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

        if (IsWhatsAppJid(trimmed))
        {
            return trimmed;
        }

        return trimmed;
    }

    private static string NormalizeHintOrFallback(string conversationHint)
    {
        if (IsWhatsAppJid(conversationHint))
        {
            return conversationHint;
        }

        return CollapseWhitespace(conversationHint);
    }

    public static bool Matches(string? expected, string? actual, string? platform = null)
    {
        _ = platform;
        var expectedKey = NormalizeExplicitKey(expected ?? string.Empty);
        var actualKey = NormalizeExplicitKey(actual ?? string.Empty);

        if (string.IsNullOrWhiteSpace(expectedKey) || string.IsNullOrWhiteSpace(actualKey))
        {
            return string.Equals(
                CollapseWhitespace(expected ?? string.Empty),
                CollapseWhitespace(actual ?? string.Empty),
                StringComparison.OrdinalIgnoreCase);
        }

        return expectedKey.Equals(actualKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
