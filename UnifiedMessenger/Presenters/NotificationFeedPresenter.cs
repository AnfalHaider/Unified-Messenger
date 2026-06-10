using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Presenters;

public sealed class NotificationFeedPresentation
{
    public IReadOnlyList<object> FeedItems { get; init; } = [];

    public bool ShowAlertList { get; init; }

    public bool ClearAllEnabled { get; init; }

    public bool MarkAllReadEnabled { get; init; }

    public int HeaderBadgeValue { get; init; }

    public bool ShowHeaderBadge { get; init; }
}

public static class NotificationFeedPresenter
{
    public static NotificationFeedPresentation BuildPresentation(
        INotificationHubService hub,
        IEnumerable<MessengerInstance>? instances = null)
    {
        ArgumentNullException.ThrowIfNull(hub);

        var instanceLookup = NotificationFeedPanelHelper.BuildInstanceLookup(instances);
        var unreadAlerts = hub.UnreadAlertCount;
        var headerBadgeValue = NotificationFeedPanelHelper.ResolveHeaderBadgeValue(unreadAlerts);
        var commandStates = NotificationFeedPanelHelper.ResolveCommandStates(
            hub.Alerts.Count,
            unreadAlerts);

        var platformGroups = NotificationFeedPanelHelper.GroupAlertsByPlatform(hub.Alerts, instanceLookup);

        return new NotificationFeedPresentation
        {
            FeedItems = NotificationFeedAlertRow.BuildFeedItems(platformGroups, instanceLookup),
            ShowAlertList = NotificationFeedPanelHelper.ShouldShowAlertList(hub.Alerts.Count),
            ClearAllEnabled = commandStates.ClearEnabled,
            MarkAllReadEnabled = commandStates.MarkAllReadEnabled,
            HeaderBadgeValue = headerBadgeValue,
            ShowHeaderBadge = headerBadgeValue > 0
        };
    }
}
