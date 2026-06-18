using System.Collections.Concurrent;

namespace UnifiedMessenger.Services;

/// <summary>
/// Holds the latest unread-based oversight data per instance, read directly from WhatsApp Web's chat
/// store: for each active chat, its unread count and last-activity time. This is WhatsApp's own "needs
/// attention" signal — reliable for every chat, no message history or name matching needed — and is the
/// command center's primary on-time source. Storing per-chat last-activity lets the date window scope
/// the metric: "of the chats active in the window, how many are caught up (no unread)".
/// </summary>
public sealed class OversightChatSnapshotService
{
    public readonly record struct ChatEntry(
        string ConversationKey,
        string CustomerName,
        int Unread,
        DateTimeOffset LastActivityUtc,
        string Preview = "",
        bool IsAwaiting = false);

    /// <summary>"Since you were last here" summary across a set of instances.</summary>
    public readonly record struct OversightDigest(
        int NewAwaiting,
        int TotalAwaiting,
        int AccountsWithAwaiting,
        DateTimeOffset? OldestActivityUtc,
        bool HasData);

    private sealed record InstanceChats(IReadOnlyList<ChatEntry> Chats, DateTimeOffset CapturedAtUtc);

    private static readonly Lazy<OversightChatSnapshotService> LazyInstance = new(() => new OversightChatSnapshotService());

    public static OversightChatSnapshotService Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<string, InstanceChats> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    private OversightChatSnapshotService()
    {
    }

    public void Update(string instanceId, IReadOnlyList<ChatEntry> chats, DateTimeOffset capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || chats is null)
        {
            return;
        }

        _byInstance[instanceId.Trim()] = new InstanceChats(chats, capturedAtUtc);
    }

    /// <summary>
    /// Active = chats whose last activity is within the window (or all chats when <paramref name="windowStartUtc"/>
    /// is null); CaughtUp = those with no unread. Returns false when there is no snapshot for the instance.
    /// </summary>
    public bool TryGetWindowed(
        string instanceId,
        DateTimeOffset? windowStartUtc,
        out int active,
        out int caughtUp,
        DateTimeOffset? windowEndUtc = null)
    {
        active = 0;
        caughtUp = 0;
        if (string.IsNullOrWhiteSpace(instanceId) || !_byInstance.TryGetValue(instanceId.Trim(), out var snap))
        {
            return false;
        }

        foreach (var chat in snap.Chats)
        {
            if (!InWindow(chat.LastActivityUtc, windowStartUtc, windowEndUtc))
            {
                continue;
            }

            active++;
            if (!chat.IsAwaiting)
            {
                caughtUp++;
            }
        }

        return true;
    }

    /// <summary>
    /// Summarize awaiting state across instances for the "since you were last here" digest: how many are
    /// awaiting in total, how many arrived since <paramref name="sinceUtc"/>, across how many accounts, and
    /// the oldest waiting activity. <c>HasData</c> is false until at least one instance has a snapshot.
    /// </summary>
    public OversightDigest BuildDigest(IEnumerable<string> instanceIds, DateTimeOffset? sinceUtc)
    {
        var total = 0;
        var fresh = 0;
        var accounts = 0;
        var hasData = false;
        DateTimeOffset? oldest = null;

        foreach (var id in instanceIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(id) || !_byInstance.TryGetValue(id.Trim(), out var snap))
            {
                continue;
            }

            hasData = true;
            var awaitingHere = 0;
            foreach (var chat in snap.Chats)
            {
                if (!chat.IsAwaiting)
                {
                    continue;
                }

                awaitingHere++;
                total++;
                if (sinceUtc is null || chat.LastActivityUtc > sinceUtc.Value)
                {
                    fresh++;
                }
                if (oldest is null || chat.LastActivityUtc < oldest.Value)
                {
                    oldest = chat.LastActivityUtc;
                }
            }

            if (awaitingHere > 0)
            {
                accounts++;
            }
        }

        return new OversightDigest(fresh, total, accounts, oldest, hasData);
    }

    private static bool InWindow(DateTimeOffset when, DateTimeOffset? startUtc, DateTimeOffset? endUtc) =>
        (startUtc is null || when >= startUtc.Value) &&
        (endUtc is null || when <= endUtc.Value);

    /// <summary>
    /// The chats awaiting a reply (unread &gt; 0) within the window, worst-first (most unread, then most
    /// recent). Used to populate the click-through "awaiting" list. Empty when there is no snapshot.
    /// </summary>
    public IReadOnlyList<ChatEntry> GetAwaiting(
        string instanceId,
        DateTimeOffset? windowStartUtc,
        DateTimeOffset? windowEndUtc = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || !_byInstance.TryGetValue(instanceId.Trim(), out var snap))
        {
            return [];
        }

        return snap.Chats
            .Where(c => c.IsAwaiting && InWindow(c.LastActivityUtc, windowStartUtc, windowEndUtc))
            .OrderByDescending(c => c.Unread)
            .ThenByDescending(c => c.LastActivityUtc)
            .ToList();
    }
}
