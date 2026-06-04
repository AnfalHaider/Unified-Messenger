using System.Collections.Concurrent;
using System.Threading.Channels;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Services;

public sealed class MessageTriageService
{
    private const int MaxQueueCapacity = 64;
    private const int MaxInferenceQueueCapacity = 32;
    private const int MaxStoredItems = 200;
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(48);

    private static readonly Lazy<MessageTriageService> LazyInstance =
        new(() => new MessageTriageService(startBackgroundWorker: true));

    private readonly ConcurrentDictionary<string, MessageTriageItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<MessageTriageRequest> _channel = Channel.CreateBounded<MessageTriageRequest>(
        new BoundedChannelOptions(MaxQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly Channel<RichTriageInferenceJob> _inferenceChannel =
        Channel.CreateBounded<RichTriageInferenceJob>(new BoundedChannelOptions(MaxInferenceQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly Task _inferenceWorker;
    private readonly MessageTriageInferenceRunner _inferenceRunner;

    private MessageTriageService(bool startBackgroundWorker, MessageTriageInferenceRunner? inferenceRunner = null)
    {
        _inferenceRunner = inferenceRunner ?? new MessageTriageInferenceRunner();
        if (startBackgroundWorker)
        {
            _worker = Task.Run(ProcessQueueAsync);
            _inferenceWorker = Task.Run(ProcessInferenceQueueAsync);
        }
        else
        {
            _worker = Task.CompletedTask;
            _inferenceWorker = Task.CompletedTask;
        }
    }

    internal MessageTriageService(MessageTriageInferenceRunner inferenceRunner)
        : this(startBackgroundWorker: false, inferenceRunner)
    {
    }

    internal MessageTriageService()
        : this(startBackgroundWorker: false)
    {
    }

    public static MessageTriageService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public void Enqueue(
        InboundMessageSelection selection,
        string? instanceDisplayName = null,
        bool allowLlmInference = true,
        bool skipDedupeCheck = false)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (string.IsNullOrWhiteSpace(selection.MessageText))
        {
            return;
        }

        if (!skipDedupeCheck &&
            !BackfillDedupeRegistry.TryAccept(
                selection.InstanceId,
                selection.Platform,
                selection.ConversationHint,
                selection.MessageText))
        {
            return;
        }

        _ = _channel.Writer.TryWrite(new MessageTriageRequest
        {
            InstanceId = selection.InstanceId,
            InstanceDisplayName = instanceDisplayName ?? selection.InstanceId,
            Platform = selection.Platform,
            MessageText = selection.MessageText.Trim(),
            CustomerName = selection.CustomerName,
            ConversationHint = selection.ConversationHint,
            TimestampUtc = selection.TimestampUtc,
            AllowLlmInference = allowLlmInference
        });
    }

    public IReadOnlyList<MessageTriageItem> GetAllItems() =>
        _items.Values
            .OrderByDescending(item => item.UrgencyScore)
            .ThenByDescending(item => item.TimestampUtc)
            .ToList();

    internal void RestoreItems(IEnumerable<MessageTriageItem> items)
    {
        _items.Clear();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            _items[item.Id] = item;
        }

        PruneExpiredItems();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public MessageTriageDashboardSnapshot BuildSnapshot(IEnumerable<MessengerInstance> professionalInstances)
    {
        var allowedIds = professionalInstances
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = _items.Values
            .Where(item => allowedIds.Contains(item.InstanceId))
            .OrderByDescending(item => item.UrgencyScore)
            .ThenByDescending(item => item.TimestampUtc)
            .ToList();

        var positive = items.Count(item => item.Sentiment == MessageSentiment.Positive);
        var neutral = items.Count(item => item.Sentiment == MessageSentiment.Neutral);
        var negative = items.Count(item => item.Sentiment == MessageSentiment.Negative);

        return new MessageTriageDashboardSnapshot
        {
            PositiveCount = positive,
            NeutralCount = neutral,
            NegativeCount = negative,
            UrgentQueue = items
                .Where(item => item.UrgencyScore >= 30)
                .Take(12)
                .ToList(),
            WeeklySentiment = BuildWeeklySentiment(items)
        };
    }

    internal void ProcessInboundForTests(InboundMessageSelection selection, string? instanceDisplayName = null)
    {
        ProcessRequest(new MessageTriageRequest
        {
            InstanceId = selection.InstanceId,
            InstanceDisplayName = instanceDisplayName ?? selection.InstanceId,
            Platform = selection.Platform,
            MessageText = selection.MessageText.Trim(),
            CustomerName = selection.CustomerName,
            ConversationHint = selection.ConversationHint,
            TimestampUtc = selection.TimestampUtc,
            AllowLlmInference = true
        });
    }

    internal async Task ProcessInferenceForTestsAsync(RichTriageInferenceJob job)
    {
        await ProcessInferenceJobAsync(job, CancellationToken.None).ConfigureAwait(false);
    }

    internal async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    ProcessRequest(request);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Message triage failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    internal async Task ProcessInferenceQueueAsync()
    {
        try
        {
            await foreach (var job in _inferenceChannel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await ProcessInferenceJobAsync(job, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Message triage inference failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private void ProcessRequest(MessageTriageRequest request)
    {
        var urgency = MessageTriageScorer.ScoreUrgency(request.MessageText, request.ConversationHint);
        var sentiment = MessageTriageScorer.ClassifySentiment(request.MessageText);
        var preview = request.MessageText.Length <= 220
            ? request.MessageText
            : request.MessageText[..217] + "...";

        var itemId = $"{request.InstanceId}|{Guid.NewGuid():N}";
        var item = new MessageTriageItem
        {
            Id = itemId,
            InstanceId = request.InstanceId,
            InstanceDisplayName = request.InstanceDisplayName,
            Platform = request.Platform,
            MessagePreview = preview,
            CustomerName = request.CustomerName,
            UrgencyScore = urgency,
            Sentiment = sentiment,
            TimestampUtc = request.TimestampUtc,
            InferenceSource = TriageInferenceSource.Heuristic
        };

        _items[item.Id] = item;
        PruneExpiredItems();
        Changed?.Invoke(this, EventArgs.Empty);

        QueueInferenceJob(request, itemId, urgency, sentiment);
    }

    private void QueueInferenceJob(
        MessageTriageRequest request,
        string triageItemId,
        int heuristicUrgency,
        MessageSentiment heuristicSentiment)
    {
        if (!request.AllowLlmInference || !AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            return;
        }

        _ = _inferenceChannel.Writer.TryWrite(new RichTriageInferenceJob
        {
            TriageItemId = triageItemId,
            InstanceId = request.InstanceId,
            InstanceDisplayName = request.InstanceDisplayName,
            Platform = request.Platform,
            MessageText = request.MessageText,
            CustomerName = request.CustomerName,
            ConversationHint = request.ConversationHint,
            TimestampUtc = request.TimestampUtc,
            HeuristicUrgencyScore = heuristicUrgency,
            HeuristicSentiment = heuristicSentiment
        });
    }

    private async Task ProcessInferenceJobAsync(RichTriageInferenceJob job, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(job.TriageItemId, out var baseline))
        {
            return;
        }

        var response = await _inferenceRunner.TryInferAsync(job, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return;
        }

        _items[job.TriageItemId] = MessageTriageInferenceRunner.ApplyInference(baseline, response);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void PruneExpiredItems()
    {
        var cutoff = DateTimeOffset.UtcNow - RetentionWindow;
        foreach (var pair in _items)
        {
            if (pair.Value.TimestampUtc < cutoff)
            {
                _items.TryRemove(pair.Key, out _);
            }
        }

        if (_items.Count <= MaxStoredItems)
        {
            return;
        }

        var overflow = _items
            .OrderBy(pair => pair.Value.TimestampUtc)
            .Take(_items.Count - MaxStoredItems)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in overflow)
        {
            _items.TryRemove(key, out _);
        }
    }

    private static IReadOnlyList<DailySentimentPoint> BuildWeeklySentiment(IReadOnlyList<MessageTriageItem> items)
    {
        var points = new List<DailySentimentPoint>();
        for (var day = 6; day >= 0; day--)
        {
            var date = DateTime.Now.Date.AddDays(-day);
            var label = date.ToString("ddd");
            var dayItems = items.Where(item => item.TimestampUtc.LocalDateTime.Date == date).ToList();
            points.Add(new DailySentimentPoint
            {
                Label = label,
                Positive = dayItems.Count(item => item.Sentiment == MessageSentiment.Positive),
                Neutral = dayItems.Count(item => item.Sentiment == MessageSentiment.Neutral),
                Negative = dayItems.Count(item => item.Sentiment == MessageSentiment.Negative)
            });
        }

        return points;
    }

    private sealed class MessageTriageRequest
    {
        public required string InstanceId { get; init; }

        public required string InstanceDisplayName { get; init; }

        public required string Platform { get; init; }

        public required string MessageText { get; init; }

        public string CustomerName { get; init; } = "Customer";

        public string ConversationHint { get; init; } = string.Empty;

        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

        public bool AllowLlmInference { get; init; } = true;
    }
}
