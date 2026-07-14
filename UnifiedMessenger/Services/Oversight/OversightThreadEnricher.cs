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

        // Prefer a real saved/display name (resolved by the scan from the contact store or sidebar) over the
        // raw number — a saved contact should show as "Muzzamil Naaz", not "+92…". Fall back to the resolved
        // phone only when we have no real name.
        string? resolvedName = null;
        if (!IsGenericName(chat.CustomerName) && chat.CustomerName.Any(char.IsLetter))
        {
            resolvedName = chat.CustomerName;
        }
        else if (!string.IsNullOrWhiteSpace(chat.ContactPhone))
        {
            resolvedName = "+" + chat.ContactPhone.TrimStart('+');
        }

        var ctx = (contextSvc ?? WhatsAppBusinessContextService.Instance)
            .GetThreadContext(instanceId, convKey);

        if (resolvedName is null && ctx is not null)
        {
            // Secondary: DOM-ingress ContactPhoneNumber (from notification pipeline or sidebar snapshot).
            if (!string.IsNullOrWhiteSpace(ctx.ContactPhoneNumber))
            {
                resolvedName = "+" + ctx.ContactPhoneNumber.TrimStart('+');
            }
            else if (!IsGenericName(ctx.CustomerName))
            {
                resolvedName = ctx.CustomerName;
            }
        }

        // Tertiary: ThreadRegistryService — notification-ingress threads keyed by title/phone.
        var threads = (registry ?? ThreadRegistryService.Instance).GetAllThreads();
        var thread = FindThread(instanceId, convKey, chat.LastActivityUtc, threads);

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

    // Threads within this window of the chat's last activity are considered timestamp matches.
    private static readonly TimeSpan TimestampMatchWindow = TimeSpan.FromSeconds(90);

    private static ThreadData? FindThread(
        string instanceId,
        string conversationKey,
        DateTimeOffset chatLastActivity,
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
        ThreadData? byTimestamp = null;
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

            // Timestamp-based fallback: when the IndexedDB chat is an @lid JID we can't match by
            // key. Find the notification-ingress thread for the same instance whose last message
            // arrived around the same time — the notification and the IndexedDB update share the
            // same real-world event so their timestamps are within seconds of each other.
            if (byTimestamp is null &&
                Math.Abs((t.LastMessageTime - chatLastActivity).TotalSeconds) <= TimestampMatchWindow.TotalSeconds)
            {
                byTimestamp = t;
            }
        }

        return fuzzy ?? byTimestamp;
    }

    private static bool IsGenericName(string? name) =>
        string.IsNullOrWhiteSpace(name) ||
        name.Equals("Customer", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("New message", StringComparison.OrdinalIgnoreCase);
}
