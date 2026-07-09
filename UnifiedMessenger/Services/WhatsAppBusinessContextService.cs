using System.Collections.Concurrent;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// In-memory store for WhatsApp DOM context and outgoing delivery telemetry per instance/thread.
/// </summary>
public sealed class WhatsAppBusinessContextService
{
    private static readonly Lazy<WhatsAppBusinessContextService> LazyInstance =
        new(() => new WhatsAppBusinessContextService());

    private readonly ConcurrentDictionary<string, WhatsAppThreadContextSnapshot> _threadContext =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, WhatsAppOutgoingStatusEvent> _latestOutgoing =
        new(StringComparer.OrdinalIgnoreCase);

    public static WhatsAppBusinessContextService Instance => LazyInstance.Value;

    internal static WhatsAppBusinessContextService CreateForTests() => new();

    public void UpsertThreadContext(WhatsAppThreadContextSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.InstanceId) ||
            string.IsNullOrWhiteSpace(snapshot.ConversationKey))
        {
            return;
        }

        var key = BuildKey(snapshot.InstanceId, snapshot.ConversationKey);
        _threadContext[key] = snapshot;
    }

    public WhatsAppThreadContextSnapshot? GetThreadContext(string instanceId, string conversationKey)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(conversationKey))
        {
            return null;
        }

        return _threadContext.TryGetValue(BuildKey(instanceId, conversationKey), out var snapshot)
            ? snapshot
            : null;
    }

    public void RecordOutgoingStatus(WhatsAppOutgoingStatusEvent statusEvent)
    {
        ArgumentNullException.ThrowIfNull(statusEvent);
        if (string.IsNullOrWhiteSpace(statusEvent.InstanceId) ||
            string.IsNullOrWhiteSpace(statusEvent.ConversationKey))
        {
            return;
        }

        var key = BuildKey(statusEvent.InstanceId, statusEvent.ConversationKey);
        _latestOutgoing[key] = statusEvent;
    }

    public WhatsAppOutgoingStatusEvent? GetLatestOutgoingStatus(string instanceId, string conversationKey)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(conversationKey))
        {
            return null;
        }

        return _latestOutgoing.TryGetValue(BuildKey(instanceId, conversationKey), out var status)
            ? status
            : null;
    }

    public void RemoveInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var prefix = instanceId.Trim() + "|";
        foreach (var key in _threadContext.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _threadContext.TryRemove(key, out _);
        }

        foreach (var key in _latestOutgoing.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _latestOutgoing.TryRemove(key, out _);
        }
    }

    private static string BuildKey(string instanceId, string conversationKey) =>
        $"{instanceId.Trim()}|{conversationKey.Trim()}";
}
