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

            // Cross-check against the client's own badge before writing anything. See
            // LooksLikeAnUnsyncedRead — this is not defensive padding, it is a measured window in which
            // the resolver reports every thread unread and says nothing about being unsure.
            var awaiting = chats.Count(x => x.IsAwaiting);
            var unreadBadge = root.TryGetProperty("unreadBadge", out var b) && b.ValueKind == JsonValueKind.Number
                ? b.GetInt32()
                : (int?)null;

            var badgeIsCapped = root.TryGetProperty("unreadBadgeCapped", out var capped) &&
                                capped.ValueKind == JsonValueKind.True;

            if (LooksLikeAnUnsyncedRead(awaiting, unreadBadge, badgeIsCapped))
            {
                AppLogger.LogWarningThrottled(
                    $"InstagramScan.{instance.Id}",
                    $"Discarded an Instagram read: {awaiting} thread(s) looked unread against the client's own badge of {unreadBadge}. "
                    + "The per-thread read and the client's own count disagree; the next pass will pick it up.",
                    "instagram-unsynced");

                // Deliberately NOT a read failure. Nothing is broken — the page is still syncing, and
                // recording a failure would tell the owner to click Re-sync, which would race the same
                // window again. Returning null leaves the previous snapshot in place, which is correct:
                // stale-by-one-cycle beats thirteen invented waiting customers.
                return null;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            OversightChatSnapshotService.Instance.Update(instance.Id, chats, nowUtc);
            AccountReadHealth.RecordSuccess(instance.Id);

            // Public activity (A13b), stored separately from the conversation snapshot above. Deliberately
            // not gated by the badge cross-check: that check is about per-thread unread state, and these
            // counts come from a different query with no per-thread claim to contradict.
            var comments = 0;
            if (root.TryGetProperty("badge", out var badge) && badge.ValueKind == JsonValueKind.Object)
            {
                comments = ActivityCount(badge, "comments");
                InstagramActivityStore.Instance.Update(
                    instance.Id,
                    comments,
                    ActivityCount(badge, "likes"),
                    ActivityCount(badge, "relationships"),
                    nowUtc);
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
    /// True when the unread count cannot be believed, because it exceeds the client's own badge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A correction, recorded because the first version of this shipped.</b> v4.99.89 justified this
    /// check with an observation that turned out to be a misreading: an account reporting 15 of 15 threads
    /// unread was assumed to be mid-sync, because a probe of "the other account" showed 2. Both probes had
    /// in fact hit the same account. The 15 were real, and the account's own title said so — as
    /// <c>(9+) Instagram</c>.
    /// </para>
    /// <para>
    /// <b>That is why the capped form matters more than the rule.</b> Instagram writes <c>(9+)</c> once the
    /// count passes nine. The first version's digits-only pattern could not parse it, read the badge as
    /// zero, and discarded every thread on the busiest account in the workspace — the precise failure this
    /// remark's own next paragraph warns about, arrived at through a regex rather than through the
    /// comparison. A capped badge is a lower bound and can never contradict anything, so it never rejects.
    /// </para>
    /// <para>
    /// <b>Exceeds, not differs.</b> The badge counts every unread thread; this reader sees the top 15 of
    /// Primary. An account with 20 unread therefore legitimately reports 15 against a badge of 20, and
    /// requiring equality would discard every busy account permanently. Only over-reporting invents
    /// waiting customers, and only over-reporting is rejected.
    /// </para>
    /// <para>
    /// <b>What it is worth keeping for.</b> No live desync has been demonstrated, so this now earns its
    /// place as a cheap consistency check rather than as a fix for a known defect: it fires only when the
    /// client's own uncapped aggregate directly contradicts the per-thread read, which would be a genuine
    /// contradiction worth refusing to publish.
    /// </para>
    /// <para>
    /// A missing badge is treated as zero rather than as unknown, because Instagram omits the prefix
    /// exactly when there is nothing unread — so "no badge with threads flagged unread" is the same
    /// contradiction in a quieter form.
    /// </para>
    /// </remarks>
    public static bool LooksLikeAnUnsyncedRead(int awaitingCount, int? unreadBadge, bool badgeIsCapped = false) =>
        !badgeIsCapped && awaitingCount > (unreadBadge ?? 0);

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

    /// <summary>
    /// One activity count off the badge record. Absent and null both read as zero: Instagram returns
    /// <c>null</c> for a category with nothing in it (<c>usertags</c> was null on both live accounts),
    /// which is the same statement as zero rather than a missing reading.
    /// </summary>
    private static int ActivityCount(JsonElement badge, string property) =>
        badge.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? Math.Max(0, value.GetInt32())
            : 0;

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
