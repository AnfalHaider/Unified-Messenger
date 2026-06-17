using System.Collections.Concurrent;

namespace UnifiedMessenger.Services;

/// <summary>
/// Holds the latest unread-based oversight snapshot per instance, read directly from WhatsApp Web's
/// chat store: how many active chats are caught up (no unread) vs awaiting a reply. This is WhatsApp's
/// own "needs attention" signal — reliable for every chat, no message history or name matching needed —
/// and is the command center's primary on-time source when present.
/// </summary>
public sealed class OversightChatSnapshotService
{
    public readonly record struct ChatSnapshot(int Active, int CaughtUp, int Awaiting, DateTimeOffset CapturedAtUtc);

    private static readonly Lazy<OversightChatSnapshotService> LazyInstance = new(() => new OversightChatSnapshotService());

    public static OversightChatSnapshotService Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<string, ChatSnapshot> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    private OversightChatSnapshotService()
    {
    }

    public void Update(string instanceId, int active, int caughtUp, int awaiting, DateTimeOffset capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        _byInstance[instanceId.Trim()] = new ChatSnapshot(active, caughtUp, awaiting, capturedAtUtc);
    }

    public bool TryGet(string instanceId, out ChatSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            return _byInstance.TryGetValue(instanceId.Trim(), out snapshot);
        }

        snapshot = default;
        return false;
    }
}
