using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace UnifiedMessenger.Services;

public sealed class TaskbarBadgeService
{
    private static readonly Lazy<TaskbarBadgeService> LazyInstance = new(() => new TaskbarBadgeService());

    public static TaskbarBadgeService Instance => LazyInstance.Value;

    public Task SyncBadgeAsync(int count)
    {
        try
        {
            var updater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();

            if (!AppSettingsService.Instance.Settings.ShowTaskbarBadge || count <= 0)
            {
                updater.Update(null);
                return Task.CompletedTask;
            }

            var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge")!;
            badgeElement.SetAttribute("value", Math.Min(count, 99).ToString());
            updater.Update(new BadgeNotification(badgeXml));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar badge update failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
