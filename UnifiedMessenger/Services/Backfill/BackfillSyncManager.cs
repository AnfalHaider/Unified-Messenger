using System.Collections.Concurrent;
using System.Diagnostics;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Services.Backfill;

public sealed class BackfillSyncManager
{
    public static readonly TimeSpan PerInstanceTimeout = TimeSpan.FromSeconds(90);

    public static readonly TimeSpan ScriptReadyDelay = TimeSpan.FromMilliseconds(1500);

    private static readonly Lazy<BackfillSyncManager> LazyInstance = new(() => new BackfillSyncManager());

    private readonly ConcurrentDictionary<string, BackfillSyncState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, BackfillResult> _lastResults =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly CancellationTokenSource _shutdownCts = new();

    private readonly IReadOnlyList<IBackfillSyncProvider> _providers;

    private BackfillSyncManager()
    {
        _providers = [new WhatsAppBackfillProvider()];
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

    public BackfillResult? GetLastResult(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        return _lastResults.TryGetValue(instanceId.Trim(), out var result) ? result : null;
    }

    public void Shutdown() => _shutdownCts.Cancel();

    /// <summary>
    /// Schedules startup backfill after adapter scripts are injected (see BasePlatformAdapter.OnNavigationCompletedAsync).
    /// </summary>
    public void Schedule(MessengerInstance instance, bool force = false)
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

        if (!PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
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

        if (!force &&
            _states.TryGetValue(instance.Id, out var existing) &&
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
        _lastResults.Clear();
        BackfillDedupeRegistry.ClearForTests();
        BackfillDedupeStore.Instance.ResetForTests();
    }

    internal void SetStateForTests(string instanceId, BackfillSyncState state) =>
        SetState(instanceId, state);

    internal static IBackfillSyncProvider? TestProviderOverride { get; set; }

    private async Task RunBackfillAsync(MessengerInstance instance, IBackfillSyncProvider provider)
    {
        try
        {
            await RunBackfillCoreAsync(instance, provider, _shutdownCts.Token).ConfigureAwait(false);
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
                SlaThresholdMinutes = settings.SlaThresholdMinutes,
                BackfillMode = settings.WhatsAppBackfillMode,
                BackfillRecentDays = settings.WhatsAppBackfillRecentDays,
                BackfillMaxChats = settings.WhatsAppBackfillMaxChats,
                EnableDeepBackfill = settings.EnableDeepBackfill,
                EnableUrgentLlmInference = settings.EnableLocalAi,
                MaxUrgentLlmPerInstance = 5
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
        StoreResult(instance.Id, result);
        RaiseProgress(instance.Id, GetState(instance.Id), result);
        if (result.IsSuccess)
        {
            DashboardRefreshCoordinator.Instance.ScheduleRefresh();
        }

        return result;
    }

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

    private void StoreResult(string instanceId, BackfillResult result) =>
        _lastResults[instanceId.Trim()] = result;

    private void RaiseProgress(string instanceId, BackfillSyncState state, BackfillResult? result = null) =>
        ProgressChanged?.Invoke(
            this,
            new BackfillProgressEventArgs(instanceId, state, result ?? GetLastResult(instanceId)));
}

public sealed class BackfillProgressEventArgs : EventArgs
{
    public BackfillProgressEventArgs(string instanceId, BackfillSyncState state, BackfillResult? result = null)
    {
        InstanceId = instanceId;
        State = state;
        Result = result;
    }

    public string InstanceId { get; }

    public BackfillSyncState State { get; }

    public BackfillResult? Result { get; }
}
