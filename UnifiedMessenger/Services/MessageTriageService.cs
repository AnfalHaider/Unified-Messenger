using System.Collections.Concurrent;
using System.Threading.Channels;
using UnifiedMessenger.Models;

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

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private MessageTriageService(bool startBackgroundWorker)
    {
        if (startBackgroundWorker)
        {
            _worker = Task.Run(ProcessQueueAsync);
        }
        else
        {
            _worker = Task.CompletedTask;
        }
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
        string? branchKey = null,
        bool allowLlmInference = true,
        bool skipDedupeCheck = false)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var cleanedMessage = ConversationNoiseFilter.CleanForInference(selection.MessageText);
        if (string.IsNullOrWhiteSpace(cleanedMessage))
        {
            return;
        }

        _ = ChannelWriteHelper.TryWriteWithDropLog(
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
                MessageKind = selection.MessageKind
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

    internal void ProcessInboundForTests(InboundMessageSelection selection, string? instanceDisplayName = null)
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
            TimestampUtc = selection.TimestampUtc
        });
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
        var messageText = ConversationNoiseFilter.CleanForInference(request.MessageText);
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return;
        }

        var isSpam = ConversationNoiseFilter.IsPromoSpam(messageText);
        var urgency = isSpam
            ? 5
            : MessageTriageScorer.ScoreUrgency(messageText, request.ConversationHint);
        var sentiment = MessageTriageScorer.ClassifySentiment(messageText);
        var preview = messageText.Length <= 220
            ? messageText
            : messageText[..217] + "...";
        if (ConversationNoiseFilter.IsDomChromePollution(preview))
        {
            return;
        }

        var branchName = BranchWorkspaceHelper.ResolveBranchKey(request.BranchKey, request.InstanceDisplayName);
        var conversationKey = ConversationKeyResolver.Resolve(
            request.Platform,
            request.ConversationKey,
            request.ConversationHint,
            request.CustomerName,
            messageText);
        var threadId = ConversationKeyResolver.BuildThreadId(request.InstanceId, conversationKey);
        var operationalUrgency = isSpam
            ? 1
            : ThreadRegistryService.MapOperationalUrgency(urgency);

        var item = new MessageTriageItem
        {
            Id = $"{request.InstanceId}|{Guid.NewGuid():N}",
            InstanceId = request.InstanceId,
            InstanceDisplayName = request.InstanceDisplayName,
            Platform = request.Platform,
            MessagePreview = preview,
            MessageFullText = messageText,
            CustomerName = request.CustomerName,
            UrgencyScore = urgency,
            Sentiment = sentiment,
            TimestampUtc = request.TimestampUtc,
            InferenceSource = TriageInferenceSource.Heuristic,
            ThreadId = threadId,
            ConversationKey = conversationKey,
            BranchName = branchName,
            OperationalUrgency = operationalUrgency,
            AiIntentCategory = isSpam
                ? UnifiedMessengerIntentCategory.Spam
                : UnifiedMessengerIntentCategory.Inquiry,
            ClientSentiment = sentiment == MessageSentiment.Negative
                ? ClientSentimentLabel.Frustrated
                : sentiment == MessageSentiment.Positive
                    ? ClientSentimentLabel.Positive
                    : ClientSentimentLabel.Neutral,
            NextActionSummary = isSpam ? "Promotional message — no action required" : string.Empty,
            SuggestedAction = isSpam ? "Ignore" : string.Empty,
            IsSpamOrPromo = isSpam,
            CustomerIntent = isSpam ? CustomerIntent.Spam : CustomerIntent.Inquiry,
            MessageKind = request.MessageKind
        };

        _items[item.Id] = item;
        ThreadRegistryService.Instance.UpsertFromTriageItem(item, conversationKey, branchName);
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

    private sealed class MessageTriageRequest
    {
        public required string InstanceId { get; init; }

        public required string InstanceDisplayName { get; init; }

        public string BranchKey { get; init; } = string.Empty;

        public required string Platform { get; init; }

        public required string MessageText { get; init; }

        public string CustomerName { get; init; } = "Customer";

        public string ConversationKey { get; init; } = string.Empty;

        public string ConversationHint { get; init; } = string.Empty;

        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

        public InboundMessageKind MessageKind { get; init; } = InboundMessageKind.Text;
    }
}
