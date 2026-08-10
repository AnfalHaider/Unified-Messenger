using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Single source of truth for turning WhatsApp Web's conversation-scan JSON into
/// <see cref="OversightChatSnapshotService.ChatEntry"/> rows. Both readers of that scan — the live oversight
/// snapshot (<see cref="OversightSnapshotReader"/>) and the startup backfill
/// (<c>WhatsAppBackfillProvider</c>) — call this, so a new scan field is wired in exactly one place instead
/// of drifting between two hand-maintained loops. (The old duplication had already dropped
/// <c>lastMessageFromMe</c> from the backfill path.)
/// </summary>
/// <remarks>
/// Everything here is parsing attacker-adjacent input: the JSON comes from a web page that can change
/// shape without notice. Two rules follow, and both are load-bearing.
/// <list type="number">
/// <item><b>A bad row must never cost a good row.</b> Both JS producers already wrap each chat in its own
/// try/catch ("skip a malformed chat rather than failing the whole scan"). This side did not, so a single
/// unexpected value threw out of the whole loop and discarded every conversation already parsed —
/// silently zeroing an account's metrics.</item>
/// <item><b>Never read a value of the wrong kind.</b> <c>JsonElement.GetString()</c> throws on a number or
/// boolean, so a field that changes type is not a missing field, it is an exception.</item>
/// </list>
/// </remarks>
public static class ChatEntryParser
{
    /// <summary>Parses the <c>conversations</c> array on a scan-result root into chat entries.</summary>
    public static List<OversightChatSnapshotService.ChatEntry> ParseConversations(JsonElement root)
    {
        var list = new List<OversightChatSnapshotService.ChatEntry>();
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("conversations", out var conversations) ||
            conversations.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        var skipped = 0;
        var awaitingInferred = 0;

        foreach (var conversation in conversations.EnumerateArray())
        {
            try
            {
                if (TryParseConversation(conversation, out var entry))
                {
                    // Non-customer conversations are filtered here, at the single point BOTH producers
                    // funnel through, rather than trusting each of them to do it. That trust was already
                    // misplaced: whatsapp-adapter.js excluded WhatsApp's own `0@c.us` notice account but
                    // whatsapp-store-bridge.js did not, so on the default path it counted as a customer
                    // awaiting a reply — and being one-way, it could never be cleared by replying.
                    if (IsNonCustomerConversation(entry.ConversationKey))
                    {
                        continue;
                    }

                    if (!HasExplicitAwaiting(conversation))
                    {
                        awaitingInferred++;
                    }

                    list.Add(entry);
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                // Belt and braces. TryParseConversation is written not to throw, but this loop must
                // survive anything the page hands us — losing one row is recoverable, losing the account's
                // whole snapshot is not.
                skipped++;
                AppLogger.LogWarning(
                    "ChatEntryParser",
                    $"Skipped an unparseable conversation row: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (skipped > 0)
        {
            AppLogger.LogWarning(
                "ChatEntryParser",
                $"Skipped {skipped} of {skipped + list.Count} conversation rows as unparseable.");
        }

        if (awaitingInferred > 0)
        {
            // Both producers emit an explicit `awaiting` today. If that ever stops, this parser silently
            // falls back to `unreadCount > 0`, which is a DIFFERENT AND WORSE definition: unread is
            // per-device read state, so reading a chat on a phone clears it without anyone having replied,
            // and it lags per linked device. Awaiting is meant to be direction-based (last message not from
            // us), which syncs identically everywhere. Silently swapping one for the other would change the
            // product's headline number, so it must never happen unnoticed.
            AppLogger.LogWarning(
                "ChatEntryParser",
                $"{awaitingInferred} conversation rows had no explicit 'awaiting' field; fell back to "
                + "unread-based inference, which is per-device and less accurate. The scraper's output shape "
                + "has probably changed.");
        }

        return list;
    }

    /// <summary>
    /// Parses a single conversation object. Returns false (and skips the row) when it carries no parseable
    /// <c>lastActivityTimestampUtc</c> — the scan always emits one, so a missing or wrong-typed timestamp
    /// means a malformed row we'd rather drop than stamp with a fabricated "now" that would pollute the
    /// activity windows. Never throws on unexpected value kinds.
    /// </summary>
    public static bool TryParseConversation(JsonElement conversation, out OversightChatSnapshotService.ChatEntry entry)
    {
        entry = default;

        if (conversation.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(ReadString(conversation, "lastActivityTimestampUtc"), out var when))
        {
            return false;
        }

        // TryGetInt32 is not as forgiving as its name suggests: it THROWS on a non-Number element rather
        // than returning false, so the ValueKind check is required, not belt-and-braces. Without it a
        // string-typed unreadCount costs the whole conversation row for no reason.
        var unread = ReadInt(conversation, "unreadCount");
        var key = ReadString(conversation, "conversationKey");
        var name = ReadString(conversation, "customerName");
        var preview = SanitizePreview(ReadString(conversation, "lastMessagePreview"));
        var awaiting = conversation.TryGetProperty("awaiting", out var a) && a.ValueKind != JsonValueKind.Null
            ? a.ValueKind == JsonValueKind.True
            : unread > 0;
        var fromMe = conversation.TryGetProperty("lastMessageFromMe", out var fm) && fm.ValueKind == JsonValueKind.True;
        var contactPhone = ReadString(conversation, "contactPhone");

        entry = new OversightChatSnapshotService.ChatEntry(
            key, name, unread, when.ToUniversalTime(), preview, awaiting, fromMe, contactPhone);
        return true;
    }

    /// <summary>
    /// True for conversation keys that are not a 1:1 customer chat and must never reach the metrics.
    /// </summary>
    /// <remarks>
    /// Groups (<c>@g.us</c>), broadcast lists and Status (<c>@broadcast</c>, which also covers
    /// <c>status@broadcast</c>), channels (<c>@newsletter</c>), and WhatsApp's own official account
    /// (<c>0@c.us</c>) are all excluded. The last one matters most and was the live defect: its messages
    /// are one-way notices you cannot reply to, so once it went unanswered it sat in the awaiting count
    /// permanently — verified in real data as "WhatsApp Business", <c>isAwaiting: true</c>, 26 days old.
    /// A leading <c>0@</c> only ever prefixes that account; real E.164 numbers have no leading-zero local
    /// part.
    /// </remarks>
    internal static bool IsNonCustomerConversation(string? conversationKey)
    {
        if (string.IsNullOrWhiteSpace(conversationKey))
        {
            return false;
        }

        var key = conversationKey.Trim().ToLowerInvariant();

        return key.Contains("@g.us", StringComparison.Ordinal)
            || key.Contains("@broadcast", StringComparison.Ordinal)
            || key.Contains("@newsletter", StringComparison.Ordinal)
            || key.StartsWith("status@", StringComparison.Ordinal)
            || key.StartsWith("0@", StringComparison.Ordinal);
    }

    /// <summary>
    /// Replaces a preview that is raw encoded media with a short human label.
    /// </summary>
    /// <remarks>
    /// Measured on real data: 85 of 3,027 stored previews (2.8%) were base64 image payloads, so the
    /// needs-reply list rendered <c>/9j/4AAQSkZJRgABAQAAAQABAAD…</c> where the README promises "the actual
    /// text of their last message". Detection is by the standard base64 signatures for JPEG (<c>/9j/</c>)
    /// and PNG (<c>iVBORw0</c>) plus data URIs — deliberately narrow, so ordinary message text that merely
    /// looks unusual is never relabelled.
    /// </remarks>
    internal static string SanitizePreview(string preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            return string.Empty;
        }

        var trimmed = preview.TrimStart();

        var looksLikeEncodedMedia =
            trimmed.StartsWith("/9j/", StringComparison.Ordinal)          // JPEG
            || trimmed.StartsWith("iVBORw0", StringComparison.Ordinal)    // PNG
            || trimmed.StartsWith("R0lGOD", StringComparison.Ordinal)     // GIF
            || trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);

        return looksLikeEncodedMedia ? "Photo" : preview;
    }

    /// <summary>True when the row carries a usable boolean <c>awaiting</c> rather than relying on inference.</summary>
    private static bool HasExplicitAwaiting(JsonElement conversation) =>
        conversation.ValueKind == JsonValueKind.Object &&
        conversation.TryGetProperty("awaiting", out var a) &&
        a.ValueKind is JsonValueKind.True or JsonValueKind.False;

    /// <summary>
    /// Reads a string property, tolerating absence, null, and the wrong value kind alike.
    /// </summary>
    /// <remarks>
    /// <c>GetString()</c> throws <see cref="InvalidOperationException"/> on a number or boolean. A scraper
    /// that starts emitting an epoch number instead of an ISO string is a realistic change, and it must
    /// degrade to "unparseable row", never to an exception that discards the whole scan.
    /// </remarks>
    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// Reads an integer property, tolerating absence, null, and the wrong value kind alike.
    /// </summary>
    /// <remarks>
    /// <c>TryGetInt32</c> throws <see cref="InvalidOperationException"/> when the element is not a Number —
    /// the <c>Try</c> prefix covers only whether the number fits in an <see cref="int"/>, not whether the
    /// element is a number at all. The <see cref="JsonValueKind"/> check is therefore load-bearing.
    /// </remarks>
    private static int ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : 0;
}
