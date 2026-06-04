using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace UnifiedMessenger.Services.Ollama;

public sealed class OllamaInferenceCoordinator : IDisposable
{
    private static readonly Lazy<OllamaInferenceCoordinator> LazyInstance =
        new(() => new OllamaInferenceCoordinator());

    private readonly Channel<InferenceWorkItem> _interactiveChannel =
        Channel.CreateUnbounded<InferenceWorkItem>(new UnboundedChannelOptions { SingleReader = true });

    private readonly Channel<InferenceWorkItem> _backgroundChannel =
        Channel.CreateUnbounded<InferenceWorkItem>(new UnboundedChannelOptions { SingleReader = true });

    private readonly SemaphoreSlim _generateGate = new(1, 1);
    private readonly CancellationTokenSource _workerCts = new();
    private readonly Task _workerTask;

    private CancellationTokenSource? _activeBackgroundCts;
    private OllamaInferenceActivity _activity = OllamaInferenceActivity.Idle;

    private OllamaInferenceCoordinator()
    {
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public static OllamaInferenceCoordinator Instance => LazyInstance.Value;

    public OllamaInferenceActivity CurrentActivity => _activity;

    public event EventHandler<OllamaInferenceActivity>? ActivityChanged;

    public async IAsyncEnumerable<string> StreamTokensAsync(
        InferencePriority priority,
        string prompt,
        string? systemPrompt = null,
        string? modelOverride = null,
        string? responseFormat = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield break;
        }

        var tokenChannel = Channel.CreateUnbounded<string>();
        var item = new InferenceWorkItem(
            priority,
            prompt,
            systemPrompt,
            modelOverride,
            responseFormat,
            tokenChannel.Writer,
            completion: null,
            cancellationToken);

        await EnqueueAsync(item, cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var token in tokenChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return token;
            }
        }
        finally
        {
            tokenChannel.Writer.TryComplete();
        }
    }

    public async Task<string?> CollectGenerateAsync(
        InferencePriority priority,
        string prompt,
        string? systemPrompt = null,
        string? modelOverride = null,
        string? responseFormat = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new InferenceWorkItem(
            priority,
            prompt,
            systemPrompt,
            modelOverride,
            responseFormat,
            tokenWriter: null,
            completion,
            cancellationToken);

        await EnqueueAsync(item, cancellationToken).ConfigureAwait(false);
        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnqueueAsync(InferenceWorkItem item, CancellationToken cancellationToken)
    {
        if (item.Priority == InferencePriority.Interactive)
        {
            CancelActiveBackgroundJob();
            await _interactiveChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _backgroundChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private void CancelActiveBackgroundJob()
    {
        var cts = _activeBackgroundCts;
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already torn down
        }
    }

    private async Task ProcessQueueAsync()
    {
        var token = _workerCts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var item = await DequeueNextAsync(token).ConfigureAwait(false);
                if (item is null)
                {
                    continue;
                }

                if (item.Priority == InferencePriority.Background)
                {
                    _activeBackgroundCts?.Dispose();
                    _activeBackgroundCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                }

                var linkedCts = item.Priority == InferencePriority.Background && _activeBackgroundCts is not null
                    ? CancellationTokenSource.CreateLinkedTokenSource(item.CancellationToken, _activeBackgroundCts.Token)
                    : CancellationTokenSource.CreateLinkedTokenSource(item.CancellationToken, token);

                try
                {
                    SetActivity(item.Priority == InferencePriority.Interactive
                        ? OllamaInferenceActivity.InteractiveStreaming
                        : OllamaInferenceActivity.BackgroundProcessing);

                    await _generateGate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                    try
                    {
                        await ExecuteWorkItemAsync(item, linkedCts.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _generateGate.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    item.CompleteCancelled();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ollama inference work failed: {ex.Message}");
                    item.CompleteError();
                }
                finally
                {
                    linkedCts.Dispose();
                    SetActivity(OllamaInferenceActivity.Idle);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task<InferenceWorkItem?> DequeueNextAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_interactiveChannel.Reader.TryRead(out var interactive))
            {
                return interactive;
            }

            if (_backgroundChannel.Reader.TryRead(out var background))
            {
                return background;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var interactiveWait = _interactiveChannel.Reader.WaitToReadAsync(linked.Token).AsTask();
            var backgroundWait = _backgroundChannel.Reader.WaitToReadAsync(linked.Token).AsTask();
            var completed = await Task.WhenAny(interactiveWait, backgroundWait).ConfigureAwait(false);

            if (completed == interactiveWait && _interactiveChannel.Reader.TryRead(out interactive))
            {
                return interactive;
            }

            if (_backgroundChannel.Reader.TryRead(out background))
            {
                return background;
            }
        }

        return null;
    }

    private static async Task ExecuteWorkItemAsync(InferenceWorkItem item, CancellationToken cancellationToken)
    {
        var orchestration = OllamaOrchestrationService.Instance;
        if (!await orchestration.EnsureEngineRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            item.CompleteEmpty();
            return;
        }

        var model = OllamaOrchestrationService.ResolveModelName(item.ModelOverride);
        var builder = new System.Text.StringBuilder();

        await foreach (var token in orchestration
                           .StreamGenerateDirectAsync(
                               model,
                               item.Prompt,
                               item.SystemPrompt,
                               item.ResponseFormat,
                               cancellationToken)
                           .ConfigureAwait(false))
        {
            builder.Append(token);
            if (item.TokenWriter is not null)
            {
                await item.TokenWriter.WriteAsync(token, cancellationToken).ConfigureAwait(false);
            }
        }

        item.TokenWriter?.TryComplete();

        if (item.Completion is not null)
        {
            item.CompleteSuccess(builder.Length == 0 ? null : builder.ToString());
        }
    }

    private void SetActivity(OllamaInferenceActivity activity)
    {
        if (_activity == activity)
        {
            return;
        }

        _activity = activity;
        ActivityChanged?.Invoke(this, activity);
    }

    public void Dispose()
    {
        _workerCts.Cancel();
        _interactiveChannel.Writer.TryComplete();
        _backgroundChannel.Writer.TryComplete();
        _activeBackgroundCts?.Cancel();
        _activeBackgroundCts?.Dispose();
        _generateGate.Dispose();
        _workerCts.Dispose();
    }

    private sealed class InferenceWorkItem
    {
        public InferenceWorkItem(
            InferencePriority priority,
            string prompt,
            string? systemPrompt,
            string? modelOverride,
            string? responseFormat,
            ChannelWriter<string>? tokenWriter,
            TaskCompletionSource<string?>? completion,
            CancellationToken cancellationToken)
        {
            Priority = priority;
            Prompt = prompt;
            SystemPrompt = systemPrompt;
            ModelOverride = modelOverride;
            ResponseFormat = responseFormat;
            TokenWriter = tokenWriter;
            Completion = completion;
            CancellationToken = cancellationToken;
        }

        public InferencePriority Priority { get; }

        public string Prompt { get; }

        public string? SystemPrompt { get; }

        public string? ModelOverride { get; }

        public string? ResponseFormat { get; }

        public ChannelWriter<string>? TokenWriter { get; }

        public TaskCompletionSource<string?>? Completion { get; }

        public CancellationToken CancellationToken { get; }

        public void CompleteSuccess(string? fullText) =>
            Completion?.TrySetResult(fullText);

        public void CompleteEmpty() =>
            Completion?.TrySetResult(null);

        public void CompleteCancelled() =>
            Completion?.TrySetCanceled();

        public void CompleteError() =>
            Completion?.TrySetResult(null);
    }
}
