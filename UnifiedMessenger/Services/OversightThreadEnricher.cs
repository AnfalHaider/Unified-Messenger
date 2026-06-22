using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Enriches IndexedDB-sourced ChatEntry records with contact names and message previews from the
/// DOM-ingress pipeline (ThreadRegistryService / WhatsAppBusinessContextService).
///
/// The IndexedDB scan sees @lid privacy IDs for unsaved contacts — the DOM ingress picks up the
/// real phone number from notifications via ContactPhoneNumber, so a join resolves both the name
/// and the preview without any @lid→phone IndexedDB research.
/// </summary>
internal static class OversightThreadEnricher
{
    /// <summary>
    /// Returns the best-available display name and message preview for an awaiting chat.
    /// Falls back to the ChatEntry's own CustomerName/Preview when no DOM-ingress record matches.
    /// </summary>
    public static (string Name, string Preview) Enrich(
        string instanceId,
        OversightChatSnapshotService.ChatEntry chat,
        ThreadRegistryService? registry = null,
        WhatsAppBusinessContextService? contextSvc = null)
    {
        var convKey = chat.ConversationKey;
        var ctx = (contextSvc ?? WhatsAppBusinessContextService.Instance)
            .GetThreadContext(instanceId, convKey);

        string? resolvedName = null;

        if (ctx is not null)
        {
            // ContactPhoneNumber is the real E.164 number captured by the adapter — it works even
            // for @lid privacy IDs which are not dialable numbers and shouldn't be displayed as-is.
            if (!string.IsNullOrWhiteSpace(ctx.ContactPhoneNumber))
            {
                resolvedName = "+" + ctx.ContactPhoneNumber.TrimStart('+');
            }
            else if (!IsGenericName(ctx.CustomerName))
            {
                resolvedName = ctx.CustomerName;
            }
        }

        // ThreadRegistryService has LastMessagePreview from the notification DOM ingress.
        var threads = (registry ?? ThreadRegistryService.Instance).GetAllThreads();
        var thread = FindThread(instanceId, convKey, threads);

        string? resolvedPreview = null;
        if (thread is not null)
        {
            if (resolvedName is null && !IsGenericName(thread.CustomerName))
            {
                resolvedName = thread.CustomerName;
            }
            if (!string.IsNullOrWhiteSpace(thread.LastMessagePreview))
            {
                resolvedPreview = thread.LastMessagePreview;
            }
        }

        return (resolvedName ?? chat.CustomerName, resolvedPreview ?? chat.Preview);
    }

    private static ThreadData? FindThread(
        string instanceId,
        string conversationKey,
        IReadOnlyList<ThreadData> threads)
    {
        var threadId = ConversationKeyResolver.BuildThreadId(instanceId, conversationKey);

        // For @c.us / @s.whatsapp.net JIDs the local part is the E.164 digits. The notification
        // ingress may have stored the thread under the bare digits (no @ suffix), so try both.
        string? phoneThreadId = null;
        var atIdx = conversationKey.IndexOf('@', StringComparison.Ordinal);
        if (atIdx > 0)
        {
            var local = conversationKey[..atIdx];
            if (local.Length is >= 6 and <= 15 && local.All(char.IsDigit))
            {
                phoneThreadId = $"{instanceId.Trim()}|{local}";
            }
        }

        ThreadData? fuzzy = null;
        foreach (var t in threads)
        {
            if (!t.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (t.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }

            if (phoneThreadId is not null &&
                t.ThreadId.Equals(phoneThreadId, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }

            if (fuzzy is null &&
                ConversationKeyResolver.Matches(conversationKey, t.ConversationKey))
            {
                fuzzy = t;
            }
        }

        return fuzzy;
    }

    private static bool IsGenericName(string? name) =>
        string.IsNullOrWhiteSpace(name) ||
        name.Equals("Customer", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("New message", StringComparison.OrdinalIgnoreCase);
}
