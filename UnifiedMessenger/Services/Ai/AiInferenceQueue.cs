using System.Collections.Concurrent;
using System.Threading.Channels;
using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ai;

namespace UnifiedMessenger.Services.Ai;

public sealed class AiInferenceQueue : IDisposable
{
    private static readonly Lazy<AiInferenceQueue> LazyInstance =
        new(() => new AiInferenceQueue(startBackgroundWorker: true));

    private readonly ConcurrentDictionary<string, AiInferenceJob> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlight =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Channel<string> _signal = Channel.CreateBounded<string>(
        new BoundedChannelOptions(OllamaOptions.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly IAiInferenceClient _inferenceClient;
    private readonly OllamaRuntimeService _runtimeService;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly IThreadRegistryService _threadRegistry;
    private readonly Func<MessageTriageService> _messageTriageProvider;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    internal AiInferenceQueue(
        bool startBackgroundWorker,
        IAiInferenceClient? inferenceClient = null,
        OllamaRuntimeService? runtimeService = null,
        Func<AppSettings>? settingsProvider = null,
        IThreadRegistryService? threadRegistry = null,
        MessageTriageService? messageTriage = null,
        Func<MessageTriageService>? messageTriageProvider = null)
    {
        _inferenceClient = inferenceClient ?? OllamaInferenceClient.Instance;
        _runtimeService = runtimeService ?? OllamaRuntimeService.Instance;
        _settingsProvider = settingsProvider ?? (() => AppSettingsService.Instance.Settings);
        _threadRegistry = threadRegistry ?? ThreadRegistryService.Instance;
        _messageTriageProvider = messageTriageProvider
                                 ?? (messageTriage is not null
                                     ? () => messageTriage
                                     : () => MessageTriageService.Instance);

        if (startBackgroundWorker)
        {
            _worker = Task.Run(ProcessQueueAsync);
        }
        else
        {
            _worker = Task.CompletedTask;
        }
    }

    public static AiInferenceQueue Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public int PendingCount => _pending.Count;

    public bool EnqueueIfEligible(MessageTriageItem item, bool allowLlmInference)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!allowLlmInference ||
            !_settingsProvider().EnableLocalAi ||
            _runtimeService.ConnectionState != OllamaConnectionState.Running ||
            item.IsSpamOrPromo ||
            (string.IsNullOrWhiteSpace(item.ThreadId) && string.IsNullOrWhiteSpace(item.ConversationKey)))
        {
            return false;
        }

        if (!IsThreadEligible(item))
        {
            return false;
        }

        var threadId = ResolveThreadId(item);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return false;
        }

        _pending[threadId] = new AiInferenceJob
        {
            ThreadId = threadId,
            TriageItemId = item.Id,
            Item = item,
            EnqueuedAtUtc = DateTimeOffset.UtcNow
        };

        _ = ChannelWriteHelper.TryWriteWithDropLog(_signal.Writer, threadId, "AiInference");
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool IsThreadInferenceActive(string threadId) =>
        !string.IsNullOrWhiteSpace(threadId) &&
        (_pending.ContainsKey(threadId) || _inFlight.ContainsKey(threadId));

    public void CancelThread(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return;
        }

        _pending.TryRemove(threadId, out _);

        if (_inFlight.TryRemove(threadId, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // shutdown race
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    public IReadOnlyList<string> SelectEligibleThreadIdsForTests(int topN = OllamaOptions.DefaultTopNUrgentThreads) =>
        SelectEligibleJobs(topN).Select(job => job.ThreadId).ToList();

    internal async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var _ in _signal.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await ProcessNextEligibleAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AI inference queue failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // shutdown
        }
    }

    internal async Task ProcessNextEligibleAsync(CancellationToken cancellationToken = default)
    {
        var job = SelectEligibleJobs().FirstOrDefault();
        if (job is null)
        {
            return;
        }

        if (!_pending.TryRemove(job.ThreadId, out _))
        {
            return;
        }

        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        jobCts.CancelAfter(OllamaOptions.InferenceTimeout);
        _inFlight[job.ThreadId] = jobCts;

        try
        {
            if (!IsThreadEligible(job.Item))
            {
                return;
            }

            if (!await _runtimeService.EnsureRunningAsync(jobCts.Token).ConfigureAwait(false))
            {
                return;
            }

            var transcript = TranscriptBuilder.Build(job.Item);
            var modelName = string.IsNullOrWhiteSpace(_settingsProvider().LocalAiModelName)
                ? OllamaOptions.DefaultModelName
                : _settingsProvider().LocalAiModelName.Trim();

            var result = await _inferenceClient
                .GenerateStructuredAsync(transcript, modelName, jobCts.Token)
                .ConfigureAwait(false);

            if (result is null || !IsThreadEligible(job.Item))
            {
                return;
            }

            _threadRegistry.EnrichFromAi(job.ThreadId, job.TriageItemId, result);
            _messageTriageProvider().ApplyAiEnrichment(job.TriageItemId, result);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine($"AI inference timed out for thread {job.ThreadId}");
        }
        finally
        {
            if (_inFlight.TryRemove(job.ThreadId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    private IReadOnlyList<AiInferenceJob> SelectEligibleJobs(
        int topN = OllamaOptions.DefaultTopNUrgentThreads)
    {
        return _pending.Values
            .Where(job => IsThreadEligible(job.Item))
            .OrderByDescending(job => job.Item.UrgencyScore)
            .ThenByDescending(job => job.Item.TimestampUtc)
            .Take(topN)
            .ToList();
    }

    private bool IsThreadEligible(MessageTriageItem item)
    {
        var threadId = ResolveThreadId(item);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return false;
        }

        var thread = _threadRegistry.GetAllThreads()
            .FirstOrDefault(candidate =>
                candidate.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase));

        return thread is not null &&
               !thread.IsReplied &&
               !thread.IsSpamOrPromo;
    }

    private static string ResolveThreadId(MessageTriageItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.ThreadId))
        {
            return item.ThreadId;
        }

        var conversationKey = ConversationKeyResolver.Resolve(
            item.Platform,
            item.ConversationKey,
            item.ConversationKey,
            item.CustomerName,
            item.MessagePreview);

        return ConversationKeyResolver.BuildThreadId(item.InstanceId, conversationKey);
    }

    public void Shutdown()
    {
        _signal.Writer.TryComplete();
        _cts.Cancel();

        foreach (var pair in _inFlight)
        {
            try
            {
                pair.Value.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore
            }
        }
    }

    internal Task WaitForShutdownAsync(TimeSpan timeout) => _worker.WaitAsync(timeout);

    public void Dispose()
    {
        Shutdown();
        _cts.Dispose();

        foreach (var pair in _inFlight)
        {
            pair.Value.Dispose();
        }

        _inFlight.Clear();
    }

    internal void ResetForTests()
    {
        _pending.Clear();
        while (_signal.Reader.TryRead(out _))
        {
        }
    }

    private sealed class AiInferenceJob
    {
        public required string ThreadId { get; init; }

        public required string TriageItemId { get; init; }

        public required MessageTriageItem Item { get; init; }

        public DateTimeOffset EnqueuedAtUtc { get; init; }
    }
}
