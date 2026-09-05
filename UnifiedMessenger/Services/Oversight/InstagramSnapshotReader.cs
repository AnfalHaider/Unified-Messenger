using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Reads Instagram's DM inbox out of the client's own Relay store and feeds
/// <see cref="OversightChatSnapshotService"/> (A13).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not part of <see cref="OversightSnapshotReader"/>.</b> That reader is the WhatsApp
/// IndexedDB pipeline: a start-and-poll scan with a watchdog, because <c>ExecuteScriptAsync</c> does not
/// await promises and a long cursor hangs the read transaction. None of that applies here. Instagram's
/// records are already in memory, so the read is one synchronous call with no watchdog, no gate and no
/// stage machine. Folding it into the WhatsApp reader would mean carrying that machinery for a channel
/// that needs none of it.
/// </para>
/// <para>
/// <b>What this deliberately cannot produce.</b> No preview text — a sweep of every record for any field
/// matching <c>snippet|last_permanent|preview_text|summary</c> comes back empty on the feed, because the
/// preview is fetched by the Direct route the app never opens. No reply timing, so Instagram is excluded
/// from the on-time denominator by its capabilities rather than scored as a miss. And only the top 15 of
/// the Primary folder: the connection is fetched <c>first:15, folder:"INBOX"</c>, General and Requests are
/// never fetched at all, and <c>has_next_page</c> is true — so the app can see that more exist without
/// being able to read them.
/// </para>
/// </remarks>
public static class InstagramSnapshotReader
{
    public readonly record struct RefreshResult(int Threads, int Awaiting, int NewComments);

    /// <summary>
    /// Reads one Instagram account. Returns null when nothing could be read, which is not the same as
    /// reading zero conversations.
    /// </summary>
    public static async Task<RefreshResult?> RefreshAsync(MessengerInstance instance)
    {
        if (instance is null ||
            string.IsNullOrWhiteSpace(instance.Id) ||
            !string.Equals(instance.Platform, "instagram", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The sign-in gate matters more on Instagram than anywhere else: a logged-out tab has no Relay
        // mailbox at all, so an ungated read returns zero threads and reports a quiet account. Same
        // reasoning as OversightSnapshotReader, and no read failure is recorded — a signed-out account is
        // not a broken one.
        if (!SignInGate.MayScan(instance.Id))
        {
            return null;
        }

        var raw = await InstanceConnection.Current
            .ExecuteScriptAsync(
                instance.Id,
                "window.__umReadInstagramThreads ? window.__umReadInstagramThreads() : 'NOFN'")
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(raw) || raw == "null")
        {
            return null;
        }

        if (raw.Contains("NOFN", StringComparison.Ordinal))
        {
            // The adapter is injected on document creation only, so this means the page has not navigated
            // yet — the ordinary lazy-loading case, not a fault. No read failure recorded, for the same
            // reason: telling the owner "can't read this account" would be wrong advice.
            AppLogger.LogWarningThrottled(
                $"InstagramScan.{instance.Id}",
                "Instagram reader is not injected on this page yet — the account's page has not loaded.",
                "instagram-not-injected");
            return null;
        }

        try
        {
            var json = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var stage = root.TryGetProperty("diag", out var diag) && diag.TryGetProperty("stage", out var s)
                ? s.GetString()
                : null;

            if (stage is not ("done" or "empty"))
            {
                AppLogger.LogWarningThrottled(
                    $"InstagramScan.{instance.Id}",
                    $"Instagram read did not complete (stage '{stage}').",
                    "instagram-stage");
                AccountReadHealth.RecordFailure(instance.Id, $"Instagram read stage '{stage}'");
                return null;
            }

            var chats = ParseConversations(root);
            var nowUtc = DateTimeOffset.UtcNow;
            OversightChatSnapshotService.Instance.Update(instance.Id, chats, nowUtc);
            AccountReadHealth.RecordSuccess(instance.Id);

            var comments = 0;
            if (root.TryGetProperty("badge", out var badge) &&
                badge.ValueKind == JsonValueKind.Object &&
                badge.TryGetProperty("comments", out var c) &&
                c.ValueKind == JsonValueKind.Number)
            {
                comments = c.GetInt32();
            }

            // Length only, never the payload — it carries customer names, and app.log is the file support
            // asks the owner to send.
            AppLogger.LogInfo(
                $"InstagramScan.{instance.Id}",
                $"Read {chats.Count} conversation(s), {chats.Count(x => x.IsAwaiting)} awaiting.");

            return new RefreshResult(chats.Count, chats.Count(x => x.IsAwaiting), comments);
        }
        catch (JsonException ex)
        {
            AppLogger.LogWarningThrottled(
                $"InstagramScan.{instance.Id}",
                $"Instagram read returned unparseable data: {ex.Message}",
                "instagram-parse");
            AccountReadHealth.RecordFailure(instance.Id, "Instagram read returned unparseable data");
            return null;
        }
    }

    /// <summary>
    /// Turns the adapter's JSON into <see cref="OversightChatSnapshotService.ChatEntry"/> values.
    /// Separated from the transport so it can be tested without a WebView.
    /// </summary>
    public static IReadOnlyList<OversightChatSnapshotService.ChatEntry> ParseConversations(JsonElement root)
    {
        var chats = new List<OversightChatSnapshotService.ChatEntry>();

        if (!root.TryGetProperty("conversations", out var conversations) ||
            conversations.ValueKind != JsonValueKind.Array)
        {
            return chats;
        }

        foreach (var item in conversations.EnumerateArray())
        {
            var key = Text(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var name = Text(item, "name");
            var username = Text(item, "username");
            var unread = Number(item, "unread");
            var awaiting = item.TryGetProperty("awaiting", out var a) && a.ValueKind == JsonValueKind.True;
            var lastMs = LongNumber(item, "lastActivityMs");

            chats.Add(new OversightChatSnapshotService.ChatEntry(
                ConversationKey: key,
                // Falls back to the handle, then to the key. A thread with no title is rare but real
                // (a brand-new request), and an empty name renders as a blank row the owner cannot act on.
                CustomerName: !string.IsNullOrWhiteSpace(name)
                    ? name
                    : !string.IsNullOrWhiteSpace(username) ? $"@{username}" : key,
                Unread: unread,
                LastActivityUtc: lastMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(lastMs)
                    : DateTimeOffset.UtcNow,
                // Empty on purpose, and permanently so on this route: the feed's Relay prefetch carries
                // thread metadata only. The surface says the text stays in Instagram rather than
                // rendering a blank preview that reads as a failed read.
                Preview: string.Empty,
                IsAwaiting: awaiting,
                LastMessageFromMe: false,
                ContactPhone: string.Empty,
                // Instagram gives no signal about whether a last message exists, and null means exactly
                // that — "this snapshot did not record it", which is not the same as false.
                HasLastMessage: null));
        }

        return chats;
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static long LongNumber(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : 0L;
}
