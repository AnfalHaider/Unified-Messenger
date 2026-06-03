using Microsoft.Windows.BadgeNotifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace UnifiedMessenger.Services;

public sealed class TaskbarBadgeService
{
    private static readonly Lazy<TaskbarBadgeService> LazyInstance = new(() => new TaskbarBadgeService());

    public static TaskbarBadgeService Instance => LazyInstance.Value;

    public Task SyncBadgeAsync(int count)
    {
        if (!AppSettingsService.Instance.Settings.ShowTaskbarBadge || count <= 0)
        {
            ClearBadge();
            return Task.CompletedTask;
        }

        var badgeCount = Math.Min(count, 99);

        if (TrySetBadgeWithAppSdk(badgeCount))
        {
            return Task.CompletedTask;
        }

        if (TrySetBadgeWithLegacyApi(badgeCount))
        {
            return Task.CompletedTask;
        }

        TaskbarOverlayService.TrySetOverlayCount(count);
        return Task.CompletedTask;
    }

    private static void ClearBadge()
    {
        try
        {
            BadgeNotificationManager.Current.ClearBadge();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BadgeNotificationManager clear failed: {ex.Message}");
        }

        try
        {
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BadgeUpdateManager clear failed: {ex.Message}");
        }

        TaskbarOverlayService.TrySetOverlayCount(0);
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
