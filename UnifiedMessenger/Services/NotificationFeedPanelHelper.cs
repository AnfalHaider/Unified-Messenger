using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class NotificationFeedPanelHelper
{
    public static int ResolveHeaderBadgeValue(int unreadAlertCount) =>
        WorkspaceSidebarHelper.ClampBadgeCount(unreadAlertCount);

    public static (bool ClearEnabled, bool MarkAllReadEnabled) ResolveCommandStates(
        int totalAlertCount,
        int unreadAlertCount) =>
        (totalAlertCount > 0, unreadAlertCount > 0);

    public static bool ShouldShowAlertList(int totalAlertCount) => totalAlertCount > 0;

    public static Dictionary<string, MessengerInstance> BuildInstanceLookup(
        IEnumerable<MessengerInstance>? instances)
    {
        if (instances is null)
        {
            return new Dictionary<string, MessengerInstance>(StringComparer.OrdinalIgnoreCase);
        }

        return instances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .GroupBy(instance => instance.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public static int CountUnreadAlerts(IEnumerable<NotificationAlert> alerts) =>
        alerts.Count(alert => !alert.IsRead);

    public static double ResolveCardOpacity(bool isRead) => isRead ? 0.72 : 1;

    public static double ResolveTitleOpacity(bool isRead) => isRead ? 0.75 : 1;

    public static double ResolveBodyOpacity(bool isRead) => isRead ? 0.65 : 0.85;

    public static string ResolveAccentColorHex(MessengerInstance? instance) =>
        instance?.AccentColor ?? PlatformBrandingHelper.DefaultAccentHex;

    public static bool IsValidAlertId(string? alertId) => !string.IsNullOrWhiteSpace(alertId);

    public static IReadOnlyList<NotificationAlertGroup> GroupAlertsByPlatform(
        IEnumerable<NotificationAlert> alerts,
        IReadOnlyDictionary<string, MessengerInstance> instanceLookup)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(instanceLookup);

        return alerts
            .GroupBy(
                alert => ResolvePlatformId(alert, instanceLookup),
                StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(
                group => PlatformDefinition.FindById(group.Key)?.DisplayName ?? group.Key,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new NotificationAlertGroup(
                group.Key,
                PlatformDefinition.FindById(group.Key)?.DisplayName ?? group.Key,
                group.OrderByDescending(alert => alert.ReceivedAt)))
            .ToList();
    }

    internal static string ResolvePlatformId(
        NotificationAlert alert,
        IReadOnlyDictionary<string, MessengerInstance> instanceLookup)
    {
        ArgumentNullException.ThrowIfNull(alert);

        var alertPlatform = string.IsNullOrWhiteSpace(alert.Platform)
            ? "generic"
            : PlatformDefinition.NormalizePlatformId(alert.Platform);

        if (instanceLookup.TryGetValue(alert.InstanceId, out var instance) &&
            !string.IsNullOrWhiteSpace(instance.Platform))
        {
            var instancePlatform = PlatformDefinition.NormalizePlatformId(instance.Platform);
            if (!string.Equals(alertPlatform, "generic", StringComparison.OrdinalIgnoreCase))
            {
                return alertPlatform;
            }

            return instancePlatform;
        }

        return alertPlatform;
    }
}
