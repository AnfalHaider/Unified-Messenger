using System.Collections.Concurrent;
using System.Threading.Channels;
using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ai;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Services;

public sealed class MessageTriageService : IMessageTriageService
{
    private const int MaxQueueCapacity = 64;
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

    private readonly AiInferenceQueue _aiInferenceQueue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private MessageTriageService(bool startBackgroundWorker, AiInferenceQueue? aiInferenceQueue = null)
    {
        _aiInferenceQueue = aiInferenceQueue ?? AiInferenceQueue.Instance;

        if (startBackgroundWorker)
        {
            _worker = Task.Run(ProcessQueueAsync);
        }
        else
        {
            _worker = Task.CompletedTask;
        }
    }

    internal MessageTriageService(AiInferenceQueue? aiInferenceQueue = null)
        : this(startBackgroundWorker: false, aiInferenceQueue)
    {
    }

    public static MessageTriageService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public void Enqueue(
        InboundMessageSelection selection,
        string? instanceDisplayName = null,
        string? branchKey = null,
        bool allowLlmInference = true,
        bool isBackfilled = false)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var cleanedMessage = ConversationNoiseFilter.CleanForInference(selection.MessageText);
        if (string.IsNullOrWhiteSpace(cleanedMessage))
        {
            return;
        }

        _ = ChannelWriteHelper.TryWriteWithDropOldest(
            _channel.Reader,
            _channel.Writer,
            new MessageTriageRequest
            {
                InstanceId = selection.InstanceId,
                InstanceDisplayName = instanceDisplayName ?? selection.InstanceId,
                BranchKey = branchKey ?? string.Empty,
                Platform = selection.Platform,
                MessageText = cleanedMessage,
                CustomerName = selection.CustomerName,
                ConversationKey = selection.ConversationKey,
                ConversationHint = selection.ConversationHint,
                TimestampUtc = selection.TimestampUtc,
                MessageKind = selection.MessageKind,
                AllowLlmInference = allowLlmInference,
                IsBackfilled = isBackfilled
            },
            "MessageTriage");
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

    internal void ResetForTests(IEnumerable<MessageTriageItem> items)
    {
        DrainPendingQueue();
        RestoreItems(items);
    }

    internal void DrainPendingQueue()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }
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

        var positive = items.Count(IsPositiveSentiment);
        var neutral = items.Count(IsNeutralSentiment);
        var negative = items.Count(IsNegativeSentiment);

        return new MessageTriageDashboardSnapshot
        {
            PositiveCount = positive,
            NeutralCount = neutral,
            NegativeCount = negative,
            UrgentQueue = items
                .Where(DashboardCardEmptyStateHelper.IsUrgentQueueItem)
                .Take(12)
                .ToList(),
            RecentInbound = items
                .Where(item => !DashboardCardEmptyStateHelper.IsUrgentQueueItem(item))
                .OrderByDescending(item => item.TimestampUtc)
                .Take(DashboardCardEmptyStateHelper.RecentInboundMaxItems)
                .ToList(),
            WeeklySentiment = BuildWeeklySentiment(items)
        };
    }

    internal void ProcessInboundForTests(
        InboundMessageSelection selection,
        string? instanceDisplayName = null,
        bool allowLlmInference = true)
    {
        ProcessRequest(new MessageTriageRequest
        {
            InstanceId = selection.InstanceId,
            InstanceDisplayName = instanceDisplayName ?? selection.InstanceId,
            Platform = selection.Platform,
            MessageText = selection.MessageText.Trim(),
            CustomerName = selection.CustomerName,
            ConversationKey = selection.ConversationKey,
            ConversationHint = selection.ConversationHint,
            TimestampUtc = selection.TimestampUtc,
            AllowLlmInference = allowLlmInference
        });
    }

    internal void ApplyAiEnrichment(string triageItemId, AiInferenceResult result)
    {
        if (string.IsNullOrWhiteSpace(triageItemId) || !_items.TryGetValue(triageItemId, out var existing))
        {
            return;
        }

        var enriched = new MessageTriageItem
        {
            Id = existing.Id,
            InstanceId = existing.InstanceId,
            InstanceDisplayName = existing.InstanceDisplayName,
            Platform = existing.Platform,
            MessagePreview = existing.MessagePreview,
            MessageFullText = existing.MessageFullText,
            CustomerName = existing.CustomerName,
            UrgencyScore = existing.UrgencyScore,
            Sentiment = existing.Sentiment,
            TimestampUtc = existing.TimestampUtc,
            InferenceSource = TriageInferenceSource.Ollama,
            ThreadId = existing.ThreadId,
            ConversationKey = existing.ConversationKey,
            BranchName = existing.BranchName,
            OperationalUrgency = existing.OperationalUrgency,
            AiIntentCategory = result.Intent,
            ClientSentiment = existing.ClientSentiment,
            CoreSummary = string.IsNullOrWhiteSpace(result.CoreSummary)
                ? result.NextAction
                : result.CoreSummary,
            NextActionSummary = result.NextAction,
            SuggestedAction = result.SuggestedAction,
            IsSpamOrPromo = existing.IsSpamOrPromo,
            CustomerIntent = existing.CustomerIntent,
            EstimatedValue = existing.EstimatedValue,
            IsRevenueLeakageRisk = existing.IsRevenueLeakageRisk,
            MessageKind = existing.MessageKind,
            IsBackfilled = existing.IsBackfilled
        };

        _items[triageItemId] = enriched;
        Changed?.Invoke(this, EventArgs.Empty);
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

    public void Shutdown()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
    }

    internal Task WaitForShutdownAsync(TimeSpan timeout) =>
        _worker.WaitAsync(timeout);

    private void ProcessRequest(MessageTriageRequest request)
    {
        var result = HeuristicTriageProcessor.Process(request);
        if (result is null)
        {
            return;
        }

        var item = result.Item;
        _items[item.Id] = item;
        ThreadRegistryService.Instance.UpsertFromTriageItem(item, result.ConversationKey, result.BranchName);
        _aiInferenceQueue.EnqueueIfEligible(item, request.AllowLlmInference);
        PruneExpiredItems();
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
                Positive = dayItems.Count(IsPositiveSentiment),
                Neutral = dayItems.Count(IsNeutralSentiment),
                Negative = dayItems.Count(IsNegativeSentiment)
            });
        }

        return points;
    }

    private static bool IsNegativeSentiment(MessageTriageItem item) =>
        item.Sentiment == MessageSentiment.Negative ||
        item.ClientSentiment.Equals(ClientSentimentLabel.Critical, StringComparison.OrdinalIgnoreCase) ||
        item.ClientSentiment.Equals(ClientSentimentLabel.Frustrated, StringComparison.OrdinalIgnoreCase);

    private static bool IsPositiveSentiment(MessageTriageItem item) =>
        item.Sentiment == MessageSentiment.Positive ||
        item.ClientSentiment.Equals(ClientSentimentLabel.Positive, StringComparison.OrdinalIgnoreCase);

    private static bool IsNeutralSentiment(MessageTriageItem item) =>
        !IsNegativeSentiment(item) && !IsPositiveSentiment(item);
}
