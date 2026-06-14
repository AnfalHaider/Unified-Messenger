using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services;

/// <summary>
/// Bounded, coalescing ingress queue for WebView2 <c>postMessage</c> payloads.
/// Parses JSON off the UI thread and marshals adapter handling back to the dispatcher.
/// </summary>
public sealed class WebMessageIngressService
{
    private const int ChannelCapacity = 256;

    private static readonly Lazy<WebMessageIngressService> LazyInstance =
        new(() => new WebMessageIngressService(startWorker: true));

    private static readonly HashSet<string> CoalesceableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        AdapterMessageTypes.BadgeCount,
        AdapterMessageTypes.Heartbeat,
        AdapterMessageTypes.WhatsAppTelemetry
    };

    private readonly Channel<WebMessageIngressWork> _channel = Channel.CreateBounded<WebMessageIngressWork>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<string, string> _coalescedPayloads =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, byte> _coalesceQueued =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private WebMessageIngressService(bool startWorker)
    {
        _worker = startWorker ? Task.Run(ProcessQueueAsync) : Task.CompletedTask;
    }

    internal WebMessageIngressService() : this(startWorker: false)
    {
    }

    public static WebMessageIngressService Instance => LazyInstance.Value;

    public int PendingCount => _channel.Reader.Count + _coalescedPayloads.Count;

    public void Enqueue(string messageJson, MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(messageJson))
        {
            return;
        }

        if (TryGetEnvelopeType(messageJson, out var messageType) &&
            CoalesceableTypes.Contains(messageType))
        {
            var coalesceKey = $"{instance.Id}|{messageType}";
            _coalescedPayloads[coalesceKey] = messageJson;

            if (!_coalesceQueued.TryAdd(coalesceKey, 0))
            {
                return;
            }

            if (!ChannelWriteHelper.TryWriteWithDropOldest(
                    _channel.Reader,
                    _channel.Writer,
                    WebMessageIngressWork.ForCoalesced(instance, coalesceKey),
                    "WebMessageIngress"))
            {
                _coalesceQueued.TryRemove(coalesceKey, out _);
            }

            return;
        }

        _ = ChannelWriteHelper.TryWriteWithDropOldest(
            _channel.Reader,
            _channel.Writer,
            WebMessageIngressWork.ForDirect(instance, messageJson),
            "WebMessageIngress");
    }

    internal async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await ProcessWorkAsync(work).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebMessage ingress failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    internal async Task ProcessWorkForTestsAsync(WebMessageIngressWork work) =>
        await ProcessWorkAsync(work).ConfigureAwait(false);

    private async Task ProcessWorkAsync(WebMessageIngressWork work)
    {
        var rawJson = work.CoalesceKey is not null &&
                      _coalescedPayloads.TryRemove(work.CoalesceKey, out var latest)
            ? latest
            : work.RawJson;

        if (work.CoalesceKey is not null)
        {
            _coalesceQueued.TryRemove(work.CoalesceKey, out _);
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = WebMessageParser.Parse(rawJson);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebMessage parse failed: {ex.Message} | Raw: {rawJson}");
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            var instance = work.Instance;
            if (!WebMessageParser.MatchesInstance(root, instance))
            {
                return;
            }

            var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
            var hub = NotificationHub.Instance;
            var clonedRoot = root.Clone();

            await UiThreadRunner.RunAsync(() =>
            {
                adapter.HandleParsedWebMessage(clonedRoot, hub, instance);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    private static bool TryGetEnvelopeType(string rawJson, out string messageType)
    {
        messageType = string.Empty;

        try
        {
            var envelope = JsonSerializer.Deserialize(rawJson, AdapterMessageJsonContext.Default.WebMessageEnvelope);
            if (!string.IsNullOrWhiteSpace(envelope?.Type))
            {
                messageType = envelope.Type;
                return true;
            }
        }
        catch (JsonException)
        {
            // Fall through to full parse for double-encoded payloads.
        }

        try
        {
            using var document = WebMessageParser.Parse(rawJson);
            if (document.RootElement.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String)
            {
                var type = typeElement.GetString();
                if (!string.IsNullOrWhiteSpace(type))
                {
                    messageType = type;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    public void Shutdown()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
    }

    internal Task WaitForShutdownAsync(TimeSpan timeout) =>
        _worker.WaitAsync(timeout);
}

internal readonly record struct WebMessageIngressWork(
    MessengerInstance Instance,
    string? RawJson,
    string? CoalesceKey)
{
    public static WebMessageIngressWork ForDirect(MessengerInstance instance, string rawJson) =>
        new(instance, rawJson, null);

    public static WebMessageIngressWork ForCoalesced(MessengerInstance instance, string coalesceKey) =>
        new(instance, null, coalesceKey);
}
