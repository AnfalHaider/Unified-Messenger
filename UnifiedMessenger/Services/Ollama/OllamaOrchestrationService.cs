using System.Runtime.CompilerServices;
using UnifiedMessenger.Models.Ollama;

namespace UnifiedMessenger.Services.Ollama;

public sealed class OllamaOrchestrationService : IDisposable
{
    private static readonly Lazy<OllamaOrchestrationService> LazyInstance =
        new(() => new OllamaOrchestrationService());

    private readonly OllamaHttpClient _apiClient;
    private readonly OllamaBootstrapService _bootstrap;
    private readonly SemaphoreSlim _engineGate = new(1, 1);

    private OllamaConnectionState _connectionState = OllamaConnectionState.Unknown;

    internal OllamaOrchestrationService(OllamaHttpClient? apiClient = null, OllamaBootstrapService? bootstrap = null)
    {
        _apiClient = apiClient ?? new OllamaHttpClient();
        _bootstrap = bootstrap ?? new OllamaBootstrapService();
    }

    public static OllamaOrchestrationService Instance => LazyInstance.Value;

    public OllamaConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState == value)
            {
                return;
            }

            _connectionState = value;
            ConnectionStateChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<OllamaConnectionState>? ConnectionStateChanged;

    public event EventHandler<OllamaPullProgress>? PullProgressChanged;

    public void WarmupInBackground()
    {
        if (!AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            return;
        }

        _ = WarmupAsync();
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureEngineRunningAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ollama warmup failed: {ex.Message}");
            SetConnectionState(OllamaConnectionState.Error);
        }
    }

    public async Task<bool> EnsureEngineRunningAsync(CancellationToken cancellationToken = default)
    {
        if (!AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            SetConnectionState(OllamaConnectionState.NotRunning);
            return false;
        }

        await _engineGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetConnectionState(OllamaConnectionState.Starting);

            if (await _apiClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
            {
                SetConnectionState(OllamaConnectionState.Running);
                return true;
            }

            var allowBootstrap = AppSettingsService.Instance.Settings.OllamaAutoBootstrap;
            var started = await _bootstrap
                .EnsureRunningAsync(_apiClient, allowBootstrap, cancellationToken)
                .ConfigureAwait(false);

            SetConnectionState(started ? OllamaConnectionState.Running : OllamaConnectionState.NotRunning);
            return started;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ollama engine ensure failed: {ex.Message}");
            SetConnectionState(OllamaConnectionState.Error);
            return false;
        }
        finally
        {
            _engineGate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ListLocalModelsAsync(CancellationToken cancellationToken = default)
    {
        if (!await EnsureEngineRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        try
        {
            return await _apiClient.ListModelNamesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ollama list models failed: {ex.Message}");
            SetConnectionState(OllamaConnectionState.Error);
            return [];
        }
    }

    public async IAsyncEnumerable<string> StreamGenerateAsync(
        string prompt,
        string? systemPrompt = null,
        string? modelOverride = null,
        string? responseFormat = null,
        InferencePriority priority = InferencePriority.Background,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield break;
        }

        if (!AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            yield break;
        }

        if (!ReferenceEquals(this, Instance))
        {
            if (!await EnsureEngineRunningAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }

            var directModel = ResolveModelName(modelOverride);
            await foreach (var token in StreamGenerateDirectAsync(
                               directModel,
                               prompt,
                               systemPrompt,
                               responseFormat,
                               cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return token;
            }

            yield break;
        }

        await foreach (var token in OllamaInferenceCoordinator.Instance
                           .StreamTokensAsync(
                               priority,
                               prompt,
                               systemPrompt,
                               modelOverride,
                               responseFormat,
                               cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return token;
        }
    }

    internal async IAsyncEnumerable<string> StreamGenerateDirectAsync(
        string model,
        string prompt,
        string? systemPrompt,
        string? responseFormat,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var token in _apiClient
                           .StreamGenerateAsync(model, prompt, systemPrompt, responseFormat, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return token;
        }
    }

    public async Task<bool> PullModelAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        if (!await EnsureEngineRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            await foreach (var progress in _apiClient
                               .StreamPullAsync(modelName, cancellationToken)
                               .ConfigureAwait(false))
            {
                PullProgressChanged?.Invoke(this, progress);
                if (!string.IsNullOrWhiteSpace(progress.Error))
                {
                    SetConnectionState(OllamaConnectionState.Error);
                    return false;
                }

                if (progress.IsComplete)
                {
                    return true;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ollama pull failed: {ex.Message}");
            SetConnectionState(OllamaConnectionState.Error);
            return false;
        }
    }

    public static string ResolveModelName(string? overrideModel)
    {
        if (!string.IsNullOrWhiteSpace(overrideModel))
        {
            return overrideModel.Trim();
        }

        var configured = AppSettingsService.Instance.Settings.LocalAiModelName;
        return string.IsNullOrWhiteSpace(configured)
            ? OllamaOptions.DefaultModelName
            : configured.Trim();
    }

    private void SetConnectionState(OllamaConnectionState state) => ConnectionState = state;

    public void Dispose()
    {
        _apiClient.Dispose();
        _bootstrap.Dispose();
        _engineGate.Dispose();
    }
}
