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
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(3);

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
        _timer.Interval = PollInterval;
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
        }
    }
}
