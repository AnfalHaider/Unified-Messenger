using System.Collections.Concurrent;
using System.Threading.Channels;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public enum UnifiedMessengerSyncEventKind
{
    ThreadResolved
}

public sealed class UnifiedMessengerSyncEvent
{
    public required UnifiedMessengerSyncEventKind Kind { get; init; }

    public required string InstanceId { get; init; }

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? Platform { get; init; }

    public string Source { get; init; } = "webview";
}

/// <summary>
/// Decouples WebView2 postMessage handlers from thread registry mutations using a bounded channel.
/// </summary>
public sealed class UnifiedMessengerStateSyncService
{
    private const int ChannelCapacity = 128;

    private static readonly Lazy<UnifiedMessengerStateSyncService> LazyInstance =
        new(() => new UnifiedMessengerStateSyncService(startWorker: true));

    private readonly Channel<UnifiedMessengerSyncEvent> _channel = Channel.CreateBounded<UnifiedMessengerSyncEvent>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentQueue<UnifiedMessengerSyncEvent> _overflow = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private UnifiedMessengerStateSyncService(bool startWorker)
    {
        _worker = startWorker ? Task.Run(ProcessQueueAsync) : Task.CompletedTask;
    }

    internal UnifiedMessengerStateSyncService() : this(startWorker: false)
    {
    }

    public static UnifiedMessengerStateSyncService Instance => LazyInstance.Value;

    public int PendingCount => _channel.Reader.Count + _overflow.Count;

    public void EnqueueThreadResolved(
        string instanceId,
        string? conversationKey,
        string? customerName,
        DateTimeOffset? resolvedAtUtc = null,
        string source = "webview",
        string? platform = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var syncEvent = new UnifiedMessengerSyncEvent
        {
            Kind = UnifiedMessengerSyncEventKind.ThreadResolved,
            InstanceId = instanceId.Trim(),
            ConversationKey = conversationKey,
            CustomerName = customerName,
            TimestampUtc = resolvedAtUtc ?? DateTimeOffset.UtcNow,
            Source = source,
            Platform = platform
        };

        if (_channel.Writer.TryWrite(syncEvent))
        {
            return;
        }

        _overflow.Enqueue(syncEvent);
    }

    internal async Task ProcessEventForTestsAsync(UnifiedMessengerSyncEvent syncEvent)
    {
        await ProcessEventAsync(syncEvent).ConfigureAwait(false);
    }

    internal async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var syncEvent in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await ProcessEventAsync(syncEvent).ConfigureAwait(false);
                    DrainOverflow();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unified Messenger sync event failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private void DrainOverflow()
    {
        while (_overflow.TryDequeue(out var syncEvent))
        {
            if (!_channel.Writer.TryWrite(syncEvent))
            {
                _overflow.Enqueue(syncEvent);
                break;
            }
        }
    }

    public void Shutdown()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
    }

    internal Task WaitForShutdownAsync(TimeSpan timeout) =>
        _worker.WaitAsync(timeout);

    private static Task ProcessEventAsync(UnifiedMessengerSyncEvent syncEvent)
    {
        switch (syncEvent.Kind)
        {
            case UnifiedMessengerSyncEventKind.ThreadResolved:
                ThreadRegistryService.Instance.MarkThreadResolved(
                    syncEvent.InstanceId,
                    syncEvent.ConversationKey,
                    syncEvent.CustomerName,
                    syncEvent.TimestampUtc,
                    syncEvent.Platform);
                UnifiedMessengerDashboardService.Instance.NotifyChanged();
                break;
        }

        return Task.CompletedTask;
    }
}
