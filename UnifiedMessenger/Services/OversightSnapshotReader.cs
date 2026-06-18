using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Runs WhatsApp Web's IndexedDB chat-store scan on an instance (start + poll, since ExecuteScriptAsync
/// doesn't await promises) and updates <see cref="OversightChatSnapshotService"/>. Shared by the command
/// center's Re-sync probe and the background <see cref="OversightAlertMonitor"/>.
/// </summary>
public static class OversightSnapshotReader
{
    public readonly record struct RefreshResult(int Active, int CaughtUp, int Awaiting);

    public static async Task<RefreshResult?> RefreshAsync(MessengerInstance instance)
    {
        if (instance is null || string.IsNullOrWhiteSpace(instance.Id))
        {
            return null;
        }

        await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                "window.__umStartDbConversationScan ? window.__umStartDbConversationScan(2000) : 'NOFN'")
            .ConfigureAwait(false);

        for (var attempt = 0; attempt < 75; attempt++) // ~22s; the scan self-settles via a 20s watchdog
        {
            await Task.Delay(300).ConfigureAwait(false);
            var raw = await InstanceSessionManager.Instance
                .TryExecuteScriptOnInstanceAsync(
                    instance.Id,
                    "window.__umGetDbConversationResult ? window.__umGetDbConversationResult() : 'NOFN'")
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw) || raw == "null" || raw == "\"\"")
            {
                continue; // not settled yet
            }

            if (raw.Contains("NOFN", StringComparison.Ordinal))
            {
                return null; // scan function not injected
            }

            try
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Deserialize<string>(raw) ?? "");
                var root = doc.RootElement;
                var stage = root.TryGetProperty("diag", out var diag) && diag.TryGetProperty("stage", out var s)
                    ? s.GetString()
                    : null;

                if (stage != "done")
                {
                    return null; // settled but unusable (e.g. watchdog-timeout) — the account is still loading
                }

                var chats = ParseChatEntries(root);
                OversightChatSnapshotService.Instance.Update(instance.Id, chats, DateTimeOffset.UtcNow);

                var active = chats.Count;
                var caughtUp = chats.Count(c => c.Unread <= 0);
                return new RefreshResult(active, caughtUp, active - caughtUp);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static List<OversightChatSnapshotService.ChatEntry> ParseChatEntries(JsonElement root)
    {
        var list = new List<OversightChatSnapshotService.ChatEntry>();
        if (!root.TryGetProperty("conversations", out var convs) || convs.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var c in convs.EnumerateArray())
        {
            var unread = c.TryGetProperty("unreadCount", out var u) && u.TryGetInt32(out var uv) ? uv : 0;
            var ts = c.TryGetProperty("lastActivityTimestampUtc", out var t) ? t.GetString() : null;
            var key = c.TryGetProperty("conversationKey", out var k) ? k.GetString() ?? "" : "";
            var name = c.TryGetProperty("customerName", out var n) ? n.GetString() ?? "" : "";
            var preview = c.TryGetProperty("lastMessagePreview", out var p) ? p.GetString() ?? "" : "";
            if (DateTimeOffset.TryParse(ts, out var when))
            {
                list.Add(new OversightChatSnapshotService.ChatEntry(key, name, unread, when.ToUniversalTime(), preview));
            }
        }

        return list;
    }
}
