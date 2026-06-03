using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class NotificationFeedPanel : UserControl
{
    private readonly NotificationHub _hub = NotificationHub.Instance;

    public event EventHandler? CollapseRequested;

    public event EventHandler<NotificationAlert>? AlertClicked;

    public NotificationFeedPanel()
    {
        InitializeComponent();
    }

    public void Refresh(NotificationHub hub, IEnumerable<MessengerInstance>? instances = null)
    {
        var instanceLookup = instances?
            .ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, MessengerInstance>(StringComparer.OrdinalIgnoreCase);

        var unreadAlerts = hub.UnreadAlertCount;
        if (unreadAlerts > 0)
        {
            HeaderBadge.Value = unreadAlerts > 99 ? 99 : unreadAlerts;
            HeaderBadge.Visibility = Visibility.Visible;
        }
        else
        {
            HeaderBadge.Visibility = Visibility.Collapsed;
        }

        ClearAllButton.IsEnabled = hub.Alerts.Count > 0;
        MarkAllReadButton.IsEnabled = hub.UnreadAlertCount > 0;

        var feedItems = new List<object>();
        foreach (var group in hub.GetAlertsGroupedByInstance())
        {
            var unread = group.Count(a => !a.IsRead);
            feedItems.Add(NotificationFeedItem.Header(group.Key, unread));

            foreach (var alert in group.OrderByDescending(a => a.ReceivedAt))
            {
                instanceLookup.TryGetValue(alert.InstanceId, out var instance);
                feedItems.Add(NotificationFeedAlertItem.FromAlert(alert, instance));
            }
        }

        AlertsList.ItemsSource = feedItems;

        if (hub.Alerts.Count > 0)
        {
            AlertsList.Visibility = Visibility.Visible;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            AlertsList.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Visible;
        }
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        CollapseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
    {
        _hub.MarkAllAlertsRead();
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        _hub.ClearAlerts();
    }

    private void DismissAlertButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string alertId })
        {
            _hub.DismissAlert(alertId);
        }
    }

    private void AlertsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NotificationFeedAlertItem { Alert: { } alert })
        {
            _hub.MarkAlertRead(alert.Id);
            AlertClicked?.Invoke(this, alert);
        }
    }

    private sealed class NotificationFeedAlertItem
    {
        public required NotificationAlert Alert { get; init; }

        public string AlertId => Alert.Id;

        public string Title => Alert.Title;

        public string Body => Alert.Body;

        public string RelativeTimeText => Alert.RelativeTimeText;

        public SolidColorBrush AccentBrush { get; init; } = new(Windows.UI.Color.FromArgb(255, 107, 114, 128));

        public double CardOpacity => Alert.IsRead ? 0.72 : 1;

        public double TitleOpacity => Alert.IsRead ? 0.75 : 1;

        public double BodyOpacity => Alert.IsRead ? 0.65 : 0.85;

        public static NotificationFeedAlertItem FromAlert(NotificationAlert alert, MessengerInstance? instance) =>
            new()
            {
                Alert = alert,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(instance?.AccentColor ?? "#6B7280")
            };
    }
}
