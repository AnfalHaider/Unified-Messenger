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

    /// <summary>The owner's label for the account this alert came from — shown on every row so a long,
    /// scrolled feed always tells you which account (not just which platform) pinged.</summary>
    public string AccountName { get; init; } = string.Empty;

    /// <summary>Two-letter initials for the row's small account avatar.</summary>
    public string AccountInitials { get; init; } = string.Empty;

    public double CardOpacity => NotificationFeedPanelHelper.ResolveCardOpacity(Alert.IsRead);

    public double TitleOpacity => NotificationFeedPanelHelper.ResolveTitleOpacity(Alert.IsRead);

    public double BodyOpacity => NotificationFeedPanelHelper.ResolveBodyOpacity(Alert.IsRead);

    public static NotificationFeedAlertRow FromAlert(NotificationAlert alert, MessengerInstance? instance)
    {
        ArgumentNullException.ThrowIfNull(alert);

        var name = instance?.DisplayName is { Length: > 0 } dn ? dn : alert.InstanceDisplayName ?? string.Empty;
        return new NotificationFeedAlertRow
        {
            Alert = alert,
            AccountName = name,
            AccountInitials = PlatformBrandingHelper.GetInitials(string.IsNullOrWhiteSpace(name) ? "Account" : name),
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
            instanceLookup.TryGetValue(group.InstanceId, out var headerInstance);
            var accentHex = NotificationFeedPanelHelper.ResolveAccentColorHex(headerInstance);
            var initials = PlatformBrandingHelper.GetInitials(string.IsNullOrWhiteSpace(group.Key) ? "Account" : group.Key);
            feedItems.Add(NotificationFeedItem.Header(group.Key, unreadCount, accentHex, initials));

            foreach (var alert in group.OrderByDescending(alert => alert.ReceivedAt))
            {
                instanceLookup.TryGetValue(alert.InstanceId, out var instance);
                feedItems.Add(FromAlert(alert, instance));
            }
        }

        return feedItems;
    }
}
