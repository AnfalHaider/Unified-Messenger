using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed class NotificationFeedAlertRow
{
    public required NotificationAlert Alert { get; init; }

    public string AlertId => Alert.Id;

    public string Title => Alert.Title;

    public string Body => Alert.Body;

    public string RelativeTimeText => Alert.RelativeTimeText;

    public SolidColorBrush AccentBrush { get; init; } = null!;

    public double CardOpacity => NotificationFeedPanelHelper.ResolveCardOpacity(Alert.IsRead);

    public double TitleOpacity => NotificationFeedPanelHelper.ResolveTitleOpacity(Alert.IsRead);

    public double BodyOpacity => NotificationFeedPanelHelper.ResolveBodyOpacity(Alert.IsRead);

    public static NotificationFeedAlertRow FromAlert(NotificationAlert alert, MessengerInstance? instance)
    {
        ArgumentNullException.ThrowIfNull(alert);

        return new NotificationFeedAlertRow
        {
            Alert = alert,
            AccentBrush = PlatformBrandingHelper.GetAccentBrush(
                NotificationFeedPanelHelper.ResolveAccentColorHex(instance))
        };
    }

    public static IReadOnlyList<object> BuildFeedItems(
        IEnumerable<NotificationAlertGroup> groups,
        IReadOnlyDictionary<string, MessengerInstance> instanceLookup)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(instanceLookup);

        var feedItems = new List<object>();
        foreach (var group in groups)
        {
            var unreadCount = NotificationFeedPanelHelper.CountUnreadAlerts(group);
            feedItems.Add(NotificationFeedItem.Header(group.Key, unreadCount));

            foreach (var alert in group.OrderByDescending(alert => alert.ReceivedAt))
            {
                instanceLookup.TryGetValue(alert.InstanceId, out var instance);
                feedItems.Add(FromAlert(alert, instance));
            }
        }

        return feedItems;
    }
}
