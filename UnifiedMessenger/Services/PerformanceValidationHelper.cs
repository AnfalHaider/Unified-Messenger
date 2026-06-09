using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Headless performance expectations used by validation harnesses and unit tests.
/// </summary>
public static class PerformanceValidationHelper
{
    public const int DashboardRefreshDebounceMilliseconds = 450;

    public const int AcceptableInstanceSwitchLatencyMilliseconds = 2000;

    public static int EstimateCoalescedRefreshCount(int burstEventsWithinDebounceWindow) =>
        burstEventsWithinDebounceWindow <= 0 ? 0 : 1;

    public static int EstimateStartupWarmTargets(
        StartupWarmMode mode,
        int instanceCount,
        bool enableLazyWebViewLoading)
    {
        if (instanceCount <= 0)
        {
            return 0;
        }

        if (enableLazyWebViewLoading ||
            mode is StartupWarmMode.Lazy or StartupWarmMode.VisibleOnly)
        {
            return 1;
        }

        return instanceCount;
    }

    public static TimeSpan EstimateStartupWarmDuration(
        StartupWarmMode mode,
        int instanceCount,
        bool enableLazyWebViewLoading,
        int millisecondsPerInstance = 350)
    {
        var targets = EstimateStartupWarmTargets(mode, instanceCount, enableLazyWebViewLoading);
        return TimeSpan.FromMilliseconds(targets * millisecondsPerInstance);
    }

    public static bool IsAcceptableInstanceSwitchLatency(TimeSpan latency) =>
        latency <= TimeSpan.FromMilliseconds(AcceptableInstanceSwitchLatencyMilliseconds);

    public static bool LazyWarmAllIsFasterThanWarmAll(int instanceCount)
    {
        if (instanceCount <= 1)
        {
            return false;
        }

        var warmAll = EstimateStartupWarmDuration(StartupWarmMode.WarmAll, instanceCount, enableLazyWebViewLoading: false);
        var visibleOnly = EstimateStartupWarmDuration(StartupWarmMode.VisibleOnly, instanceCount, enableLazyWebViewLoading: false);
        return visibleOnly < warmAll;
    }
}
