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

    /// <summary>Empty-state heading. Never contradicts the sidebar badge — see the presenter.</summary>
    public string EmptyTitle { get; init; } = EmptyStateNoAlerts;

    /// <summary>Empty-state supporting line.</summary>
    public string EmptyHint { get; init; } = EmptyHintDefault;

    internal const string EmptyStateNoAlerts = "No notifications yet.";

    internal const string EmptyHintDefault =
        "Unread counts appear on the sidebar. New messages show up here as your accounts sync.";
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

        var accountGroups = NotificationFeedPanelHelper.GroupAlertsByInstance(hub.Alerts, instanceLookup);

        // The sidebar badge and this panel count DIFFERENT THINGS under one label, and said so out loud.
        // The badge is NotificationHub.TotalUnreadCount — unread MESSAGES summed across unmuted accounts.
        // This panel lists ALERTS the hub has raised. Both were right; on screen they read as one quantity
        // disagreeing with itself: "Notification Hub 21" in the rail beside "No notifications yet." here.
        //
        // Not a counting bug, so it is not fixed by changing either number. The empty state simply has to
        // stop denying what the badge is showing, and name what that badge actually counts.
        var unreadMessages = hub.TotalUnreadCount;
        var hasUnreadMessages = unreadMessages > 0;

        return new NotificationFeedPresentation
        {
            FeedItems = NotificationFeedAlertRow.BuildFeedItems(accountGroups, instanceLookup),
            ShowAlertList = NotificationFeedPanelHelper.ShouldShowAlertList(hub.Alerts.Count),
            ClearAllEnabled = commandStates.ClearEnabled,
            MarkAllReadEnabled = commandStates.MarkAllReadEnabled,
            HeaderBadgeValue = headerBadgeValue,
            ShowHeaderBadge = headerBadgeValue > 0,
            EmptyTitle = hasUnreadMessages
                ? "Nothing needs your attention here."
                : NotificationFeedPresentation.EmptyStateNoAlerts,
            EmptyHint = hasUnreadMessages
                ? $"The {unreadMessages} on the sidebar {(unreadMessages == 1 ? "is an unread message" : "are unread messages")}, "
                  + "not alerts. This panel fills up when something needs attention."
                : NotificationFeedPresentation.EmptyHintDefault
        };
    }
}
