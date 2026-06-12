using Microsoft.UI.Dispatching;

namespace UnifiedMessenger.Services;

/// <summary>
/// Single debounced entry point for dashboard operational refreshes (OCC + personal metrics).
/// </summary>
public sealed class DashboardRefreshCoordinator
{
    private const int DebounceMilliseconds = 500;

    private static readonly Lazy<DashboardRefreshCoordinator> LazyInstance =
        new(() => new DashboardRefreshCoordinator());

    private readonly object _subscriptionGate = new();
    private int _subscriptionCount;
    private bool _wireOperationalSources = true;
    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _debounceTimer;

    private DashboardRefreshCoordinator()
    {
    }

    public static DashboardRefreshCoordinator Instance => LazyInstance.Value;

    internal static DashboardRefreshCoordinator CreateForTests() =>
        new() { _wireOperationalSources = false };

    public event EventHandler? RefreshRequested;

    public void Attach(DispatcherQueue dispatcherQueue) =>
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));

    public void Subscribe()
    {
        lock (_subscriptionGate)
        {
            if (_subscriptionCount++ > 0)
            {
                return;
            }
        }

        if (!_wireOperationalSources)
        {
            return;
        }

        NotificationHub.Instance.Changed += OnOperationalSourceChanged;
        MessageTriageService.Instance.Changed += OnOperationalSourceChanged;
        ThreadRegistryService.Instance.Changed += OnOperationalSourceChanged;
        UnifiedMessengerDashboardService.Instance.Changed += OnOperationalSourceChanged;
        MessageAnalyticsService.Instance.Changed += OnOperationalSourceChanged;
    }

    public void Unsubscribe()
    {
        lock (_subscriptionGate)
        {
            if (_subscriptionCount == 0)
            {
                return;
            }

            _subscriptionCount--;
            if (_subscriptionCount > 0)
            {
                return;
            }
        }

        if (!_wireOperationalSources)
        {
            return;
        }

        NotificationHub.Instance.Changed -= OnOperationalSourceChanged;
        MessageTriageService.Instance.Changed -= OnOperationalSourceChanged;
        ThreadRegistryService.Instance.Changed -= OnOperationalSourceChanged;
        UnifiedMessengerDashboardService.Instance.Changed -= OnOperationalSourceChanged;
        MessageAnalyticsService.Instance.Changed -= OnOperationalSourceChanged;

        _debounceTimer?.Stop();
    }

    public void ScheduleRefresh() => StartDebounceTimer();

    public void RequestImmediateRefresh() => RaiseRefreshRequested();

    private void OnOperationalSourceChanged(object? sender, EventArgs e) => ScheduleRefresh();

    private void StartDebounceTimer()
    {
        if (_dispatcherQueue is null)
        {
            RaiseRefreshRequested();
            return;
        }

        _debounceTimer ??= _dispatcherQueue.CreateTimer();
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds);
        _debounceTimer.Tick -= OnDebounceTick;
        _debounceTimer.Tick += OnDebounceTick;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        RaiseRefreshRequested();
    }

    private void RaiseRefreshRequested() => RefreshRequested?.Invoke(this, EventArgs.Empty);
}
