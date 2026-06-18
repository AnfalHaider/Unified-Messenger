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
        DateTimeOffset LastActivityUtc);

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
    public bool TryGetWindowed(string instanceId, DateTimeOffset? windowStartUtc, out int active, out int caughtUp)
    {
        active = 0;
        caughtUp = 0;
        if (string.IsNullOrWhiteSpace(instanceId) || !_byInstance.TryGetValue(instanceId.Trim(), out var snap))
        {
            return false;
        }

        foreach (var chat in snap.Chats)
        {
            if (windowStartUtc is not null && chat.LastActivityUtc < windowStartUtc.Value)
            {
                continue;
            }

            active++;
            if (chat.Unread <= 0)
            {
                caughtUp++;
            }
        }

        return true;
    }

    /// <summary>
    /// The chats awaiting a reply (unread &gt; 0) within the window, worst-first (most unread, then most
    /// recent). Used to populate the click-through "awaiting" list. Empty when there is no snapshot.
    /// </summary>
    public IReadOnlyList<ChatEntry> GetAwaiting(string instanceId, DateTimeOffset? windowStartUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || !_byInstance.TryGetValue(instanceId.Trim(), out var snap))
        {
            return [];
        }

        return snap.Chats
            .Where(c => c.Unread > 0 &&
                        (windowStartUtc is null || c.LastActivityUtc >= windowStartUtc.Value))
            .OrderByDescending(c => c.Unread)
            .ThenByDescending(c => c.LastActivityUtc)
            .ToList();
    }
}
