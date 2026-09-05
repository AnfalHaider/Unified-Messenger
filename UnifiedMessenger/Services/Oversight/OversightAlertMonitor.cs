using Microsoft.UI.Dispatching;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Background oversight monitor: periodically re-reads each connected professional account's unread
/// snapshot (keeping the command-center numbers fresh between manual re-syncs) and fires a desktop toast
/// when an account's awaiting-reply count crosses a threshold — so the owner is told without watching.
/// Edge-triggered per account to avoid repeat spam.
/// </summary>
public sealed class OversightAlertMonitor
{
    public const int DefaultAwaitingThreshold = 5;

    // Cadence is adaptive because the two readers cost wildly different amounts. The IndexedDB scan does a
    // bounded getAll over the whole chat store behind a 20s watchdog, so 90s is about as often as it can
    // reasonably run. The store bridge is a synchronous read of already-loaded in-memory models — cheap
    // enough to run far more often, which is what actually makes awaiting counts feel live.
    internal static readonly TimeSpan LegacyPollInterval = TimeSpan.FromSeconds(90);
    internal static readonly TimeSpan BridgePollInterval = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Poll on the fast cadence only once the bridge is actually carrying every account we're polling.
    /// A mixed fleet (one account on the bridge, one fallen back to IndexedDB) keeps the slow cadence, so
    /// we never hammer the expensive reader just because a different account happens to be cheap.
    /// </summary>
    internal static TimeSpan ResolvePollInterval(int bridgeActive, int attempted) =>
        attempted > 0 && bridgeActive == attempted ? BridgePollInterval : LegacyPollInterval;

    private static readonly Lazy<OversightAlertMonitor> LazyInstance = new(() => new OversightAlertMonitor());

    public static OversightAlertMonitor Instance => LazyInstance.Value;

    private readonly Dictionary<string, bool> _alerted = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherQueue? _ui;
    private DispatcherQueueTimer? _timer;
    private IInstanceRegistryService? _registry;
    private bool _started;
    private bool _running;

    private OversightAlertMonitor()
    {
    }

    /// <summary>Awaiting-reply count that triggers an alert; 0 disables. Read from settings each pass.</summary>
    public int Threshold =>
        AppSettingsService.Instance.Settings.OversightAwaitingAlertThreshold;

    public void Start(IInstanceRegistryService registry, DispatcherQueue ui)
    {
        if (_started || registry is null || ui is null)
        {
            return;
        }

        _started = true;
        _registry = registry;
        _ui = ui;

        _timer = ui.CreateTimer();
        _timer.Interval = LegacyPollInterval; // starts cautious; speeds up once the bridge proves itself
        _timer.Tick += (_, _) => _ = TickAsync();
        _timer.Start();

        _ = TickAsync(); // initial pass
    }

    /// <summary>Edge-trigger: fire only when crossing up to/over the threshold; reset once back below.</summary>
    internal static (bool Fire, bool Alerted) Evaluate(int awaiting, int threshold, bool alerted) =>
        awaiting >= threshold ? (!alerted, true) : (false, false);

    private async Task TickAsync()
    {
        if (_running || _registry is null)
        {
            return;
        }

        _running = true;
        try
        {
            var pros = _registry.Instances.Where(instance => instance.IsProfessional).ToList();
            foreach (var instance in pros)
            {
                if (InstanceConnectionStatusService.Instance.GetStatus(instance.Id) != InstanceConnectionStatus.Connected)
                {
                    continue;
                }

                // Keep the message-count analytics (activity graph) fresh on their own — throttled internally
                // to its own (slower) cadence, and it runs a separate scan that won't clobber the read below.
                Backfill.BackfillSyncManager.Instance.SchedulePeriodicAnalyticsRefresh(instance);

                // Instagram reads its own Relay store rather than the WhatsApp IndexedDB pipeline, so it
                // has its own reader. Routed by platform rather than tried-and-fallen-through, because
                // running the WhatsApp scan against an Instagram page produces a "scan function is not
                // injected" warning on every cycle — the exact log noise the Google Business gate above
                // this method exists to prevent.
                if (string.Equals(instance.Platform, "instagram", StringComparison.OrdinalIgnoreCase))
                {
                    await InstagramSnapshotReader.RefreshAsync(instance).ConfigureAwait(true);
                    continue;
                }

                var result = await OversightSnapshotReader.RefreshAsync(instance).ConfigureAwait(true);
                if (result is null)
                {
                    continue;
                }

                var threshold = Threshold;
                if (threshold <= 0)
                {
                    continue; // alerts disabled — snapshot still refreshed above
                }

                // Quiet hours: don't ping overnight. Skip before Evaluate so the edge is preserved and the
                // alert fires once quiet hours end (rather than being consumed silently).
                if (QuietHours.IsQuietNow(AppSettingsService.Instance.Settings))
                {
                    continue;
                }

                var awaiting = result.Value.Awaiting;
                var alreadyAlerted = _alerted.TryGetValue(instance.Id, out var a) && a;
                var (fire, alerted) = Evaluate(awaiting, threshold, alreadyAlerted);
                _alerted[instance.Id] = alerted;

                if (!fire)
                {
                    continue;
                }

                var name = instance.DisplayName;
                var id = instance.Id;
                var count = awaiting;
                _ui?.TryEnqueue(() => AppNotificationService.Instance.ShowInfoToast(
                    $"{name}: {count} awaiting reply",
                    count == 1 ? "1 customer is waiting for a response." : $"{count} customers are waiting for a response.",
                    id));
            }
        }
        finally
        {
            _running = false;
            ApplyAdaptiveCadence();
        }
    }

    /// <summary>
    /// Retunes the timer after each pass. Done here rather than at Start because whether the bridge works
    /// isn't knowable until we've actually tried it against a loaded page.
    /// </summary>
    private void ApplyAdaptiveCadence()
    {
        if (_timer is null)
        {
            return;
        }

        var interval = ResolvePollInterval(StoreBridgeHealth.ActiveCount, StoreBridgeHealth.AttemptedCount);
        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }
    }
}
