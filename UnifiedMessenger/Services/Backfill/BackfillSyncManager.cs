using System.Collections.Concurrent;
using System.Diagnostics;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

public sealed class BackfillSyncManager
{
    public const int DefaultMaxAiInferenceJobs = 5;

    public static readonly TimeSpan PerInstanceTimeout = TimeSpan.FromSeconds(90);

    public static readonly TimeSpan ScriptReadyDelay = TimeSpan.FromMilliseconds(1500);

    private static readonly Lazy<BackfillSyncManager> LazyInstance = new(() => new BackfillSyncManager());

    private readonly ConcurrentDictionary<string, BackfillSyncState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<IBackfillSyncProvider> _providers;

    private BackfillSyncManager()
    {
        _providers =
        [
            new WhatsAppBackfillProvider(),
            new MetaGoogleBackfillProvider()
        ];
    }

    public static BackfillSyncManager Instance => LazyInstance.Value;

    public event EventHandler<BackfillProgressEventArgs>? ProgressChanged;

    public BackfillSyncState GetState(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return BackfillSyncState.NotStarted;
        }

        return _states.TryGetValue(instanceId.Trim(), out var state)
            ? state
            : BackfillSyncState.NotStarted;
    }

    /// <summary>
    /// Schedules startup backfill after adapter scripts are injected (see BasePlatformAdapter.OnNavigationCompletedAsync).
    /// </summary>
    public void Schedule(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!instance.IsProfessional)
        {
            SetState(instance.Id, BackfillSyncState.Skipped);
            return;
        }

        if (!AppSettingsService.Instance.Settings.EnableStartupBackfill)
        {
            SetState(instance.Id, BackfillSyncState.Skipped);
            return;
        }

        var provider = ResolveProvider(instance);
        if (provider is null)
        {
            SetState(instance.Id, BackfillSyncState.Skipped);
            return;
        }

        if (_states.TryGetValue(instance.Id, out var existing) &&
            existing is BackfillSyncState.Running or BackfillSyncState.Completed)
        {
            return;
        }

        SetState(instance.Id, BackfillSyncState.NotStarted);
        _ = Task.Run(() => RunBackfillAsync(instance, provider));
    }

    internal async Task<BackfillResult> RunBackfillForTestsAsync(
        MessengerInstance instance,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(instance)
            ?? throw new InvalidOperationException($"No backfill provider for {instance.Platform}.");

        return await RunBackfillCoreAsync(instance, provider, cancellationToken).ConfigureAwait(false);
    }

    internal void ResetStateForTests()
    {
        _states.Clear();
        BackfillDedupeRegistry.ClearForTests();
    }

    internal void SetStateForTests(string instanceId, BackfillSyncState state) =>
        SetState(instanceId, state);

    private async Task RunBackfillAsync(MessengerInstance instance, IBackfillSyncProvider provider)
    {
        try
        {
            await RunBackfillCoreAsync(instance, provider, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Backfill failed for {instance.Id}: {ex.Message}");
            SetState(instance.Id, BackfillSyncState.Failed);
        }
    }

    private async Task<BackfillResult> RunBackfillCoreAsync(
        MessengerInstance instance,
        IBackfillSyncProvider provider,
        CancellationToken cancellationToken)
    {
        SetState(instance.Id, BackfillSyncState.Running);
        RaiseProgress(instance.Id, BackfillSyncState.Running);

        BackfillResult result;
        try
        {
            await Task.Delay(ScriptReadyDelay, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(PerInstanceTimeout);

            var settings = AppSettingsService.Instance.Settings;
            var context = new BackfillContext
            {
                Instance = instance,
                EnableLocalAi = settings.EnableLocalAi,
                MaxAiInferenceJobs = DefaultMaxAiInferenceJobs,
                SlaThresholdMinutes = settings.SlaThresholdMinutes
            };

            result = await provider.RunAsync(context, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new BackfillResult { ErrorMessage = "Backfill timed out." };
        }
        catch (Exception ex)
        {
            result = new BackfillResult { ErrorMessage = ex.Message };
        }

        SetState(instance.Id, result.IsSuccess ? BackfillSyncState.Completed : BackfillSyncState.Failed);
        RaiseProgress(instance.Id, GetState(instance.Id));
        return result;
    }

    internal static IBackfillSyncProvider? TestProviderOverride { get; set; }

    private IBackfillSyncProvider? ResolveProvider(MessengerInstance instance)
    {
        if (TestProviderOverride is not null && TestProviderOverride.CanBackfill(instance))
        {
            return TestProviderOverride;
        }

        foreach (var provider in _providers)
        {
            if (provider.CanBackfill(instance))
            {
                return provider;
            }
        }

        return null;
    }

    private void SetState(string instanceId, BackfillSyncState state) =>
        _states[instanceId.Trim()] = state;

    private void RaiseProgress(string instanceId, BackfillSyncState state) =>
        ProgressChanged?.Invoke(this, new BackfillProgressEventArgs(instanceId, state));
}

public sealed class BackfillProgressEventArgs(string instanceId, BackfillSyncState state) : EventArgs
{
    public string InstanceId { get; } = instanceId;

    public BackfillSyncState State { get; } = state;
}
