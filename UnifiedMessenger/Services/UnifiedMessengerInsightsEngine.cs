using System.Threading.Channels;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

public enum UnifiedMessengerInsightsJobKind
{
    MessageAnalysis,
    ThreadRefresh
}

public sealed class UnifiedMessengerInsightsJob
{
    public required UnifiedMessengerInsightsJobKind Kind { get; init; }

    public required string InstanceId { get; init; }

    public string? TriageItemId { get; init; }

    public string? ThreadId { get; init; }
}

/// <summary>
/// Background analytical scanner that enriches triage output and refreshes hanging-lead flags.
/// </summary>
public sealed class UnifiedMessengerInsightsEngine
{
    private const int ChannelCapacity = 48;
    private static readonly TimeSpan ThreadRefreshInterval = TimeSpan.FromMinutes(3);

    private static readonly Lazy<UnifiedMessengerInsightsEngine> LazyInstance =
        new(() => new UnifiedMessengerInsightsEngine(startBackgroundWorker: true));

    private readonly Channel<UnifiedMessengerInsightsJob> _channel = Channel.CreateBounded<UnifiedMessengerInsightsJob>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly Task _refreshLoop;
    private readonly MessageTriageInferenceRunner _inferenceRunner;
    private readonly MessageTriageService _triageService;

    private UnifiedMessengerInsightsEngine(
        bool startBackgroundWorker,
        MessageTriageInferenceRunner? inferenceRunner = null,
        MessageTriageService? triageService = null)
    {
        _inferenceRunner = inferenceRunner ?? new MessageTriageInferenceRunner();
        _triageService = triageService ?? MessageTriageService.Instance;
        if (startBackgroundWorker)
        {
            _worker = Task.Run(ProcessQueueAsync);
            _refreshLoop = Task.Run(RunThreadRefreshLoopAsync);
        }
        else
        {
            _worker = Task.CompletedTask;
            _refreshLoop = Task.CompletedTask;
        }
    }

    internal UnifiedMessengerInsightsEngine(MessageTriageInferenceRunner inferenceRunner, MessageTriageService triageService)
        : this(startBackgroundWorker: false, inferenceRunner, triageService)
    {
    }

    internal UnifiedMessengerInsightsEngine(MessageTriageInferenceRunner inferenceRunner)
        : this(startBackgroundWorker: false, inferenceRunner)
    {
    }

    internal UnifiedMessengerInsightsEngine() : this(startBackgroundWorker: false)
    {
    }

    public static UnifiedMessengerInsightsEngine Instance => LazyInstance.Value;

    public void EnqueueMessageAnalysis(string instanceId, string triageItemId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(triageItemId))
        {
            return;
        }

        _ = _channel.Writer.TryWrite(new UnifiedMessengerInsightsJob
        {
            Kind = UnifiedMessengerInsightsJobKind.MessageAnalysis,
            InstanceId = instanceId.Trim(),
            TriageItemId = triageItemId.Trim()
        });
    }

    public void EnqueueThreadRefresh(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return;
        }

        _ = _channel.Writer.TryWrite(new UnifiedMessengerInsightsJob
        {
            Kind = UnifiedMessengerInsightsJobKind.ThreadRefresh,
            InstanceId = threadId.Split('|')[0],
            ThreadId = threadId.Trim()
        });
    }

    internal async Task ProcessJobForTestsAsync(UnifiedMessengerInsightsJob job) =>
        await ProcessJobAsync(job, CancellationToken.None).ConfigureAwait(false);

    internal async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await ProcessJobAsync(job, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unified Messenger insights job failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task RunThreadRefreshLoopAsync()
    {
        using var timer = new PeriodicTimer(ThreadRefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
            {
                ThreadRegistryService.Instance.RefreshOperationalFlags();
                UnifiedMessengerDashboardService.Instance.NotifyChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task ProcessJobAsync(UnifiedMessengerInsightsJob job, CancellationToken cancellationToken)
    {
        await TriageInferencePriorityBroker.WaitForBackgroundSlotAsync(cancellationToken).ConfigureAwait(false);

        switch (job.Kind)
        {
            case UnifiedMessengerInsightsJobKind.MessageAnalysis:
                await AnalyzeMessageAsync(job, cancellationToken).ConfigureAwait(false);
                break;
            case UnifiedMessengerInsightsJobKind.ThreadRefresh:
                RefreshThread(job.ThreadId);
                break;
        }
    }

    private async Task AnalyzeMessageAsync(UnifiedMessengerInsightsJob job, CancellationToken cancellationToken)
    {
        var items = _triageService.GetAllItems();
        var item = items.FirstOrDefault(i =>
            i.Id.Equals(job.TriageItemId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        var transcript = await TryBuildTranscriptAsync(job.InstanceId, cancellationToken).ConfigureAwait(false);
        RichTriageInferenceJob inferenceJob = new()
        {
            TriageItemId = item.Id,
            InstanceId = item.InstanceId,
            InstanceDisplayName = item.InstanceDisplayName,
            Platform = item.Platform,
            MessageText = item.MessagePreview,
            CustomerName = item.CustomerName,
            ConversationHint = item.ConversationKey,
            TimestampUtc = item.TimestampUtc,
            HeuristicUrgencyScore = item.UrgencyScore,
            HeuristicSentiment = item.Sentiment,
            ConversationTranscript = transcript ?? string.Empty
        };

        RichTriageLlmResponse? llmResponse = null;
        if (AppSettingsService.Instance.Settings.EnableLocalAi &&
            item.InferenceSource != TriageInferenceSource.LocalAi)
        {
            llmResponse = await _inferenceRunner.TryInferAsync(inferenceJob, cancellationToken).ConfigureAwait(false);
        }

        MessageTriageItem enrichedItem;
        if (llmResponse is not null)
        {
            enrichedItem = MessageTriageInferenceRunner.ApplyInference(
                item,
                UnifiedMessengerInsightsAnalyzer.Enrich(llmResponse, inferenceJob, transcript));
        }
        else if (item.InferenceSource == TriageInferenceSource.LocalAi)
        {
            enrichedItem = MessageTriageInferenceRunner.ApplyInference(
                item,
                UnifiedMessengerInsightsAnalyzer.Enrich(ToLlmResponse(item), inferenceJob, transcript));
        }
        else
        {
            enrichedItem = UnifiedMessengerInsightsAnalyzer.ApplyOperationalInsights(item);
        }

        _triageService.ReplaceItemForInsights(enrichedItem);
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            enrichedItem,
            enrichedItem.ConversationKey,
            enrichedItem.BranchName,
            enrichedItem.NextActionSummary,
            enrichedItem.AiIntentCategory,
            enrichedItem.ClientSentiment,
            enrichedItem.OperationalUrgency,
            enrichedItem.EstimatedValue,
            enrichedItem.IsRevenueLeakageRisk);
        UnifiedMessengerDashboardService.Instance.NotifyChanged();
    }

    private static void RefreshThread(string? threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return;
        }

        ThreadRegistryService.Instance.RefreshOperationalFlags();
        UnifiedMessengerDashboardService.Instance.NotifyChanged();
    }

    private static RichTriageLlmResponse ToLlmResponse(MessageTriageItem item) =>
        new()
        {
            UrgencyScore = item.UrgencyScore,
            Sentiment = item.Sentiment.ToString(),
            CustomerIntent = item.CustomerIntent.ToString(),
            ExtractedEntities = item.ExtractedEntities,
            CoreSummary = item.CoreSummary,
            AiIntentCategory = item.AiIntentCategory,
            ClientSentiment = item.ClientSentiment,
            OperationalUrgency = item.OperationalUrgency,
            EstimatedValue = item.EstimatedValue,
            NextActionSummary = item.NextActionSummary,
            IsRevenueLeakageRisk = item.IsRevenueLeakageRisk
        };

    private static async Task<string?> TryBuildTranscriptAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = await ConversationContextScraper
                .ExtractAsync(instanceId, maxMessages: 6, cancellationToken)
                .ConfigureAwait(false);
            if (context is null || !context.Ok || context.Messages.Count == 0)
            {
                return null;
            }

            return ConversationNoiseFilter.BuildTranscript(
                context.Messages.Select(message => (message.Direction, message.Text)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Insights transcript scrape skipped: {ex.Message}");
            return null;
        }
    }
}
