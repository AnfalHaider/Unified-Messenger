using Microsoft.Windows.BadgeNotifications;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class TaskbarBadgeService : ITaskbarBadgeService
{
    private static readonly Lazy<TaskbarBadgeService> LazyInstance = new(() => new TaskbarBadgeService());

    private readonly object _gate = new();
    private int _lastAppliedCount = -1;
    private bool _lastAppliedVisible;

    public static TaskbarBadgeService Instance => LazyInstance.Value;

    internal static int NormalizeBadgeCount(int count) =>
        count <= 0 ? 0 : Math.Min(count, 99);

    internal static bool ShouldDisplayBadge(AppSettings settings, int count) =>
        settings.ShowTaskbarBadge && count > 0;

    public Task SyncBadgeAsync(int count)
    {
        var settings = AppSettingsService.Instance.Settings;
        var visible = ShouldDisplayBadge(settings, count);
        var badgeCount = NormalizeBadgeCount(count);

        lock (_gate)
        {
            if (visible == _lastAppliedVisible && badgeCount == _lastAppliedCount)
            {
                return Task.CompletedTask;
            }

            if (!visible)
            {
                ClearAllBadgeSurfaces();
                _lastAppliedVisible = false;
                _lastAppliedCount = 0;
                return Task.CompletedTask;
            }

            bool applied;
            if (TrySetBadgeWithAppSdk(badgeCount))
            {
                TaskbarOverlayService.ClearOverlay();
                applied = true;
            }
            else
            {
                ClearAppSdkBadge();
                applied = TaskbarOverlayService.TrySetOverlayCount(badgeCount);
            }

            // Only remember what was actually applied. This used to record success unconditionally, so a
            // badge attempted before the window existed — which is normal during startup — marked itself
            // as done, and every later call with the same count short-circuited on the cache and never
            // retried. The badge would then stay absent until the number happened to change.
            if (!applied)
            {
                return Task.CompletedTask;
            }

            _lastAppliedVisible = true;
            _lastAppliedCount = badgeCount;
        }

        return Task.CompletedTask;
    }

    internal static void ClearAllBadgeSurfaces()
    {
        ClearAppSdkBadge();
        TaskbarOverlayService.ClearOverlay();
    }

    /// <summary>
    /// Set once the Windows App SDK badge API has proved unavailable, so it is not retried.
    /// </summary>
    /// <remarks>
    /// It fails on this app's shipping configuration — unpackaged plus <c>WindowsAppSDKSelfContained</c> —
    /// with "A method was called at an unexpected time", the same family of problem as
    /// <see cref="AppNotificationService"/>: the badge platform expects an identity and runtime support a
    /// self-contained unpackaged build does not carry. Retrying it on every unread-count change achieved
    /// nothing except two identical warnings per change once those warnings became real log lines. The
    /// taskbar overlay is the correct Win32 mechanism here anyway, and is now what actually runs.
    /// </remarks>
    private static bool _appSdkBadgeUnavailable;

    private static void ClearAppSdkBadge()
    {
        if (_appSdkBadgeUnavailable)
        {
            return;
        }

        try
        {
            BadgeNotificationManager.Current.ClearBadge();
        }
        catch (Exception ex)
        {
            _appSdkBadgeUnavailable = true;
            AppLogger.LogInfo(
                "Notifications.Badge",
                $"Badge API unavailable ({ex.Message.Trim()}); using the taskbar overlay instead.");
        }
    }

    private static bool TrySetBadgeWithAppSdk(int badgeCount)
    {
        if (_appSdkBadgeUnavailable)
        {
            return false;
        }

        try
        {
            BadgeNotificationManager.Current.SetBadgeAsCount((uint)badgeCount);
            return true;
        }
        catch (Exception ex)
        {
            _appSdkBadgeUnavailable = true;
            AppLogger.LogInfo(
                "Notifications.Badge",
                $"Badge API unavailable ({ex.Message.Trim()}); using the taskbar overlay instead.");
            return false;
        }
    }

    /// <summary>Test seam: forget that the SDK badge API failed.</summary>
    internal static void ResetAvailabilityForTests() => _appSdkBadgeUnavailable = false;
}
