using System.Collections.Concurrent;

namespace UnifiedMessenger.Services;

/// <summary>
/// In-process publish/subscribe for <see cref="IChannelEvent"/>. One place every channel reports to, and
/// one place consumers subscribe from, instead of each consumer reaching into each adapter.
/// </summary>
/// <remarks>
/// Publishing is fire-and-forget and never throws back at the publisher: a scraper must not fail because
/// a dashboard handler threw. Handlers run synchronously on the publishing thread, so UI subscribers must
/// marshal to the UI thread themselves via <c>DispatcherQueue.TryEnqueue</c> — the same rule the AI
/// callbacks already follow.
/// </remarks>
public sealed class ChannelEventBus
{
    private static readonly Lazy<ChannelEventBus> LazyInstance = new(() => new ChannelEventBus());

    public static ChannelEventBus Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<Guid, Action<IChannelEvent>> _handlers = new();

    private ChannelEventBus()
    {
    }

    /// <summary>Subscribes to every channel event. Dispose the returned token to unsubscribe.</summary>
    public IDisposable Subscribe(Action<IChannelEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var token = Guid.NewGuid();
        _handlers[token] = handler;
        return new Subscription(this, token);
    }

    /// <summary>Subscribes to one event type only — the common case, so callers don't hand-write type tests.</summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : IChannelEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe(evt =>
        {
            if (evt is T typed)
            {
                handler(typed);
            }
        });
    }

    public void Publish(IChannelEvent channelEvent)
    {
        if (channelEvent is null)
        {
            return;
        }

        foreach (var handler in _handlers.Values)
        {
            try
            {
                handler(channelEvent);
            }
            catch (Exception ex)
            {
                // A broken subscriber must never take down the scraper that published.
                AppLogger.LogWarning("ChannelEvents", $"Channel event handler failed: {ex.Message}");
            }
        }
    }

    public int SubscriberCount => _handlers.Count;

    internal void Clear() => _handlers.Clear();

    private sealed class Subscription(ChannelEventBus bus, Guid token) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            bus._handlers.TryRemove(token, out _);
        }
    }
}
