using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Single source of truth for turning WhatsApp Web's IndexedDB conversation-scan JSON into
/// <see cref="OversightChatSnapshotService.ChatEntry"/> rows. Both readers of that scan — the live oversight
/// snapshot (<see cref="OversightSnapshotReader"/>) and the startup backfill
/// (<c>WhatsAppBackfillProvider</c>) — call this, so a new scan field is wired in exactly one place instead
/// of drifting between two hand-maintained loops. (The old duplication had already dropped
/// <c>lastMessageFromMe</c> from the backfill path.)
/// </summary>
public static class ChatEntryParser
{
    /// <summary>Parses the <c>conversations</c> array on a scan-result root into chat entries.</summary>
    public static List<OversightChatSnapshotService.ChatEntry> ParseConversations(JsonElement root)
    {
        var list = new List<OversightChatSnapshotService.ChatEntry>();
        if (!root.TryGetProperty("conversations", out var conversations) ||
            conversations.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var conversation in conversations.EnumerateArray())
        {
            if (TryParseConversation(conversation, out var entry))
            {
                list.Add(entry);
            }
        }

        return list;
    }

    /// <summary>
    /// Parses a single conversation object. Returns false (and skips the row) when it carries no parseable
    /// <c>lastActivityTimestampUtc</c> — the scan always emits one, so a missing timestamp means a malformed
    /// row we'd rather drop than stamp with a fabricated "now" that would pollute the activity windows.
    /// </summary>
    public static bool TryParseConversation(JsonElement conversation, out OversightChatSnapshotService.ChatEntry entry)
    {
        entry = default;

        var timestamp = conversation.TryGetProperty("lastActivityTimestampUtc", out var t) ? t.GetString() : null;
        if (!DateTimeOffset.TryParse(timestamp, out var when))
        {
            return false;
        }

        var unread = conversation.TryGetProperty("unreadCount", out var u) && u.TryGetInt32(out var uv) ? uv : 0;
        var key = conversation.TryGetProperty("conversationKey", out var k) ? k.GetString() ?? "" : "";
        var name = conversation.TryGetProperty("customerName", out var n) ? n.GetString() ?? "" : "";
        var preview = conversation.TryGetProperty("lastMessagePreview", out var p) ? p.GetString() ?? "" : "";
        var awaiting = conversation.TryGetProperty("awaiting", out var a)
            ? a.ValueKind == JsonValueKind.True
            : unread > 0;
        var fromMe = conversation.TryGetProperty("lastMessageFromMe", out var fm) && fm.ValueKind == JsonValueKind.True;
        var contactPhone = conversation.TryGetProperty("contactPhone", out var cp) ? cp.GetString() ?? "" : "";

        entry = new OversightChatSnapshotService.ChatEntry(
            key, name, unread, when.ToUniversalTime(), preview, awaiting, fromMe, contactPhone);
        return true;
    }
}
