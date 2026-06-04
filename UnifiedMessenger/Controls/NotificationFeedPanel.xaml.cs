using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class NotificationFeedPanel : UserControl
{
    private NotificationHub _hub = NotificationHub.Instance;

    public event EventHandler? CollapseRequested;

    public event EventHandler<NotificationAlert>? AlertClicked;

    public NotificationFeedPanel()
    {
        InitializeComponent();
    }

    public void Refresh(NotificationHub hub, IEnumerable<MessengerInstance>? instances = null)
    {
        ArgumentNullException.ThrowIfNull(hub);

        _hub = hub;
        var instanceLookup = NotificationFeedPanelHelper.BuildInstanceLookup(instances);

        var unreadAlerts = hub.UnreadAlertCount;
        var headerBadgeValue = NotificationFeedPanelHelper.ResolveHeaderBadgeValue(unreadAlerts);
        if (headerBadgeValue > 0)
        {
            HeaderBadge.Value = headerBadgeValue;
            HeaderBadge.Visibility = Visibility.Visible;
        }
        else
        {
            HeaderBadge.Visibility = Visibility.Collapsed;
        }

        var commandStates = NotificationFeedPanelHelper.ResolveCommandStates(
            hub.Alerts.Count,
            unreadAlerts);
        ClearAllButton.IsEnabled = commandStates.ClearEnabled;
        MarkAllReadButton.IsEnabled = commandStates.MarkAllReadEnabled;

        AlertsList.ItemsSource = NotificationFeedAlertRow.BuildFeedItems(
            hub.GetAlertsGroupedByInstance(),
            instanceLookup);

        if (NotificationFeedPanelHelper.ShouldShowAlertList(hub.Alerts.Count))
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

    private void CollapseButton_Click(object sender, RoutedEventArgs e) =>
        CollapseRequested?.Invoke(this, EventArgs.Empty);

    private void MarkAllReadButton_Click(object sender, RoutedEventArgs e) =>
        _hub.MarkAllAlertsRead();

    private void ClearAllButton_Click(object sender, RoutedEventArgs e) =>
        _hub.ClearAlerts();

    private void DismissAlertButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string alertId } &&
            NotificationFeedPanelHelper.IsValidAlertId(alertId))
        {
            _hub.DismissAlert(alertId.Trim());
        }
    }

    private void AlertsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not NotificationFeedAlertRow { Alert: { } alert })
        {
            return;
        }

        _hub.MarkAlertRead(alert.Id);
        AlertClicked?.Invoke(this, alert);
    }
}
