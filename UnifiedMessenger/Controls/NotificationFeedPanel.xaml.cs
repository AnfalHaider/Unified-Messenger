using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class NotificationFeedPanel : UserControl
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;
    private INotificationHubService _hub;

    public NotificationFeedViewModel ViewModel { get; } = new();

    public event EventHandler? CollapseRequested;

    public event EventHandler<NotificationAlert>? AlertClicked;

    public NotificationFeedPanel()
    {
        _hub = _services.NotificationHub;
        InitializeComponent();
    }

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _hub = services.NotificationHub;
    }

    public void Refresh(INotificationHubService hub, IEnumerable<MessengerInstance>? instances = null)
    {
        ArgumentNullException.ThrowIfNull(hub);

        _hub = hub;
        ApplyPresentation(NotificationFeedPresenter.BuildPresentation(hub, instances));
        ApplyHubStatus(instances);
    }

    /// <summary>
    /// Says why the hub may be quieter than expected — quiet hours, and accounts that cannot raise an
    /// alert at all. See <see cref="NotificationHubStatus"/> for why an empty hub is ambiguous without it.
    /// </summary>
    private void ApplyHubStatus(IEnumerable<MessengerInstance>? instances)
    {
        var accounts = instances?.ToList() ?? _services?.Registry.Instances.ToList();

        var text = NotificationHubStatus.Describe(
            _services?.AppSettings.Settings,
            SignInGate.CountSignedOut(accounts),
            DateTime.Now.Hour);

        if (string.IsNullOrWhiteSpace(text))
        {
            HubStatusText.Visibility = Visibility.Collapsed;
            return;
        }

        HubStatusText.Text = text;
        HubStatusText.Visibility = Visibility.Visible;
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

        ViewModel.FeedItems.Clear();
        foreach (var item in presentation.FeedItems)
        {
            ViewModel.FeedItems.Add(item);
        }

        AlertsList.ItemsSource = ViewModel.FeedItems;
        AlertsList.Visibility = presentation.ShowAlertList ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = presentation.ShowAlertList ? Visibility.Collapsed : Visibility.Visible;

        // Set from the presentation rather than fixed in XAML, so this panel can never say "No notifications
        // yet." while the sidebar badge beside it shows a number.
        EmptyStatePanel.Title = presentation.EmptyTitle;
        EmptyStatePanel.Hint = presentation.EmptyHint;
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

        if (await confirm.ShowManagedAsync() != ContentDialogResult.Primary)
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


