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

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAnalyticsRefreshUtc =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, byte> _analyticsRefreshInFlight =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How often the background analytics (message counts / activity graph) refresh on their own.</summary>
    public static readonly TimeSpan AnalyticsRefreshInterval = TimeSpan.FromMinutes(8);

    /// <summary>
    /// Throttled background refresh of the message-count analytics for a connected account, so the activity
    /// graph updates on its own between manual Re-syncs. Runs ONLY the message-aggregate scan (separate JS
    /// global from the oversight snapshot scan, so no clobbering). No-op if a full backfill is running for the
    /// account, an analytics refresh is already in flight, or one ran within <see cref="AnalyticsRefreshInterval"/>.
    /// WhatsApp only (the only platform with a message-store aggregate scan today).
    /// </summary>
    public void SchedulePeriodicAnalyticsRefresh(MessengerInstance instance)
    {
        if (instance is null || !instance.IsProfessional)
        {
            return;
        }

        if (!AppSettingsService.Instance.Settings.EnableStartupBackfill)
        {
            return;
        }

        var platform = instance.Platform ?? string.Empty;
        if (!platform.Equals("whatsapp", StringComparison.OrdinalIgnoreCase) &&
            !platform.Equals("whatsappbusiness", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // A full backfill uses the same __umMsgAgg global — don't overlap it.
        if (GetState(instance.Id) == BackfillSyncState.Running)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastAnalyticsRefreshUtc.TryGetValue(instance.Id, out var last) && now - last < AnalyticsRefreshInterval)
        {
            return;
        }

        if (!_analyticsRefreshInFlight.TryAdd(instance.Id, 1))
        {
            return; // one is already running for this account
        }

        _lastAnalyticsRefreshUtc[instance.Id] = now;
        var id = instance.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
                cts.CancelAfter(TimeSpan.FromSeconds(45));
                await WhatsAppBackfillProvider.RefreshMessageAggregatesAsync(id, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Periodic analytics refresh failed for {id}: {ex.Message}");
            }
            finally
            {
                _analyticsRefreshInFlight.TryRemove(id, out _);
            }
        });
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
