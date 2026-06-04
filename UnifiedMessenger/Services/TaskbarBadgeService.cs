using Microsoft.Windows.BadgeNotifications;
using UnifiedMessenger.Models;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace UnifiedMessenger.Services;

public sealed class TaskbarBadgeService
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

            if (TrySetBadgeWithAppSdk(badgeCount))
            {
                ClearLegacyBadge();
                TaskbarOverlayService.ClearOverlay();
            }
            else if (TrySetBadgeWithLegacyApi(badgeCount))
            {
                ClearAppSdkBadge();
                TaskbarOverlayService.ClearOverlay();
            }
            else
            {
                ClearAppSdkBadge();
                ClearLegacyBadge();
                TaskbarOverlayService.TrySetOverlayCount(badgeCount);
            }

            _lastAppliedVisible = true;
            _lastAppliedCount = badgeCount;
        }

        return Task.CompletedTask;
    }

    internal static void ClearAllBadgeSurfaces()
    {
        ClearAppSdkBadge();
        ClearLegacyBadge();
        TaskbarOverlayService.ClearOverlay();
    }

    private static void ClearAppSdkBadge()
    {
        try
        {
            BadgeNotificationManager.Current.ClearBadge();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BadgeNotificationManager clear failed: {ex.Message}");
        }
    }

    private static void ClearLegacyBadge()
    {
        try
        {
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BadgeUpdateManager clear failed: {ex.Message}");
        }
    }

    private static bool TrySetBadgeWithAppSdk(int badgeCount)
    {
        try
        {
            BadgeNotificationManager.Current.SetBadgeAsCount((uint)badgeCount);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BadgeNotificationManager update failed: {ex.Message}");
            return false;
        }
    }

    private static bool TrySetBadgeWithLegacyApi(int badgeCount)
    {
        try
        {
            var updater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
            var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge")!;
            badgeElement.SetAttribute("value", badgeCount.ToString());
            updater.Update(new BadgeNotification(badgeXml));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BadgeUpdateManager update failed: {ex.Message}");
            return false;
        }
    }
}
