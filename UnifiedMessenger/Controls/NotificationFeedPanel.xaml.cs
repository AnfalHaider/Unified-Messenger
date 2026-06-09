using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class NotificationFeedPanel : UserControl
{
    private INotificationHubService _hub = NotificationHub.Instance;

    public NotificationFeedViewModel ViewModel { get; } = new();

    public event EventHandler? CollapseRequested;

    public event EventHandler<NotificationAlert>? AlertClicked;

    public NotificationFeedPanel()
    {
        InitializeComponent();
    }

    public void Refresh(INotificationHubService hub, IEnumerable<MessengerInstance>? instances = null)
    {
        ArgumentNullException.ThrowIfNull(hub);

        _hub = hub;
        ApplyPresentation(NotificationFeedPresenter.BuildPresentation(hub, instances));
    }

    private void ApplyPresentation(NotificationFeedPresentation presentation)
    {
        ViewModel.ClearAllEnabled = presentation.ClearAllEnabled;
        ViewModel.MarkAllReadEnabled = presentation.MarkAllReadEnabled;
        ViewModel.ShowAlertList = presentation.ShowAlertList;
        ViewModel.HeaderBadgeValue = presentation.HeaderBadgeValue;
        ViewModel.HeaderBadgeVisibility = presentation.ShowHeaderBadge
            ? Visibility.Visible
            : Visibility.Collapsed;

        ClearAllButton.IsEnabled = presentation.ClearAllEnabled;
        MarkAllReadButton.IsEnabled = presentation.MarkAllReadEnabled;

        if (presentation.ShowHeaderBadge)
        {
            HeaderBadge.Value = presentation.HeaderBadgeValue;
            HeaderBadge.Visibility = Visibility.Visible;
        }
        else
        {
            HeaderBadge.Visibility = Visibility.Collapsed;
        }

        ViewModel.AlertRows.Clear();
        foreach (var row in presentation.AlertRows)
        {
            ViewModel.AlertRows.Add(row);
        }

        AlertsList.ItemsSource = ViewModel.AlertRows;
        AlertsList.Visibility = presentation.ShowAlertList ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = presentation.ShowAlertList ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e) =>
        CollapseRequested?.Invoke(this, EventArgs.Empty);

    private void MarkAllReadButton_Click(object sender, RoutedEventArgs e) =>
        _hub.MarkAllAlertsRead();

    private async void ClearAllButton_Click(object sender, RoutedEventArgs e) =>
        await ClearAllWithConfirmationAsync();

    internal async Task ClearAllWithConfirmationAsync()
    {
        if (_hub.Alerts.Count == 0)
        {
            return;
        }

        if (XamlRoot is null)
        {
            _hub.ClearAlerts();
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Clear all notifications?",
            Content = "This removes every alert from the notification panel and resets unread sidebar badges.",
            PrimaryButtonText = "Clear all",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _hub.ClearAlerts();
    }

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
