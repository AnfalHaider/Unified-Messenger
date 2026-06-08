using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class PersonalOverviewPanel : UserControl
{
    private const int RefreshDebounceMilliseconds = 300;

    private IEnumerable<MessengerInstance> _personalInstances = [];
    private string? _quickActionInstanceId;
    private DispatcherQueueTimer? _refreshDebounceTimer;

    public PersonalOverviewPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Content is ScrollViewer rootScrollViewer)
        {
            ScrollInputHelper.EnableVerticalScrollBubbling(SummaryCardsGrid, rootScrollViewer);
        }
    }

    public void Refresh(IEnumerable<MessengerInstance> personalInstances)
    {
        _personalInstances = personalInstances;
        ApplySnapshot(BuildSnapshot(), GlobalSearchBox.Text);
    }

    public void ScheduleRefresh(IEnumerable<MessengerInstance> personalInstances)
    {
        _personalInstances = personalInstances;
        _refreshDebounceTimer ??= DispatcherQueue.CreateTimer();
        _refreshDebounceTimer.Interval = TimeSpan.FromMilliseconds(RefreshDebounceMilliseconds);
        _refreshDebounceTimer.Tick -= OnRefreshDebounceTick;
        _refreshDebounceTimer.Tick += OnRefreshDebounceTick;
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;

        if (_refreshDebounceTimer is not null)
        {
            _refreshDebounceTimer.Tick -= OnRefreshDebounceTick;
            _refreshDebounceTimer.Stop();
            _refreshDebounceTimer = null;
        }
    }

    private void OnRefreshDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        ApplySnapshot(BuildSnapshot(), GlobalSearchBox.Text);
    }

    private PersonalDashboardSnapshot BuildSnapshot() =>
        PersonalDashboardService.Instance.BuildSnapshot(
            _personalInstances,
            NotificationHub.Instance,
            InstanceSessionManager.Instance,
            ResourceMonitorService.Instance,
            AdapterHealthMonitor.Instance,
            InstanceConnectionStatusService.Instance);

    private void ApplySnapshot(PersonalDashboardSnapshot snapshot, string? searchQuery)
    {
        var viewState = PersonalDashboardPresentationHelper.BuildViewState(snapshot, searchQuery);
        _quickActionInstanceId = viewState.QuickAction.InstanceId;

        ActiveAccountsValue.Text = viewState.PersonalAccountCount.ToString();
        UnreadValue.Text = viewState.TotalUnreadCount.ToString();
        MemoryValue.Text = $"{viewState.AppWorkingSetMegabytes} MB";
        VisibleAccountValue.Text = viewState.VisibleInstanceName;
        PersonalLastUpdatedText.Text = viewState.LastUpdatedText;

        if (viewState.QuickAction.IsVisible)
        {
            OpenBusiestInboxButton.Content = viewState.QuickAction.Label;
            OpenBusiestInboxButton.Visibility = Visibility.Visible;
        }
        else
        {
            OpenBusiestInboxButton.Visibility = Visibility.Collapsed;
        }

        RecentActivityList.ItemsSource = viewState.FilteredActivity
            .Select(item => new PersonalOverviewActivityViewItem
            {
                Alert = item.Alert,
                Title = item.Title,
                Body = item.Body,
                InstanceDisplayName = item.InstanceDisplayName,
                RelativeTimeText = item.RelativeTimeText,
                IconGlyph = item.IconGlyph,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(item.AccentColorHex),
                UnreadIndicatorVisibility = item.IsUnread ? Visibility.Visible : Visibility.Collapsed
            })
            .ToList();

        RecentActivityEmptyTitle.Text = viewState.ActivityEmptyState.Title;
        RecentActivityEmptyText.Text = viewState.ActivityEmptyState.Hint;
        RecentActivityEmptyIcon.Glyph = viewState.ActivityEmptyState.IconGlyph;
        RecentActivityEmptyPanel.Visibility = viewState.ShowActivityList
            ? Visibility.Collapsed
            : Visibility.Visible;
        RecentActivityList.Visibility = viewState.ShowActivityList
            ? Visibility.Visible
            : Visibility.Collapsed;

        InstanceTilesList.ItemsSource = viewState.InstanceTiles
            .Select(tile => new PersonalOverviewTileViewItem
            {
                InstanceId = tile.InstanceId,
                DisplayName = tile.DisplayName,
                PlatformLabel = tile.PlatformLabel,
                DetailLine = tile.DetailLine,
                ConnectionStatusLabel = tile.ConnectionStatusLabel,
                ConnectionBrush = PlatformBrandingHelper.GetAccentBrush(tile.ConnectionColorHex),
                IconGlyph = tile.IconGlyph,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(tile.AccentColorHex),
                MutedIndicatorVisibility = tile.IsMuted ? Visibility.Visible : Visibility.Collapsed,
                UnreadBadgeVisibility = tile.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed,
                UnreadBadgeText = tile.UnreadCount == 1 ? "1 unread" : $"{tile.UnreadCount} unread"
            })
            .ToList();

        InstanceTilesEmptyText.Text = viewState.InstanceTilesEmptyHint;
        InstanceTilesEmptyPanel.Visibility = viewState.ShowInstanceTilesEmptyState
            ? Visibility.Visible
            : Visibility.Collapsed;
        InstanceTilesList.Visibility = viewState.ShowInstanceTilesEmptyState
            ? Visibility.Collapsed
            : Visibility.Visible;

        UpdateSearchSuggestions(searchQuery);
    }

    private HashSet<string> PersonalInstanceIds =>
        _personalInstances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .Select(instance => instance.Id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void GlobalSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            return;
        }

        ApplySnapshot(BuildSnapshot(), sender.Text);
    }

    private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is PersonalOverviewSearchSuggestion suggestion)
        {
            NavigateFromSearchSuggestion(suggestion);
            return;
        }

        var query = args.QueryText?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var suggestions = BuildSearchSuggestions(query);
        if (suggestions.Count > 0)
        {
            NavigateFromSearchSuggestion(suggestions[0]);
        }
    }

    private void GlobalSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is PersonalOverviewSearchSuggestion suggestion)
        {
            NavigateFromSearchSuggestion(suggestion);
        }
    }

    private void UpdateSearchSuggestions(string? query) =>
        GlobalSearchBox.ItemsSource = BuildSearchSuggestions(query);

    private List<PersonalOverviewSearchSuggestion> BuildSearchSuggestions(string? query)
    {
        var personalAlerts = NotificationHub.Instance.Alerts
            .Where(alert => PersonalInstanceIds.Contains(alert.InstanceId));

        return DashboardPageHelper
            .FilterPersonalSearchMatches(_personalInstances, query, personalAlerts)
            .Select(match => new PersonalOverviewSearchSuggestion
            {
                Label = match.Label,
                SubLabel = match.SubLabel,
                InstanceId = match.InstanceId,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(match.AccentColorHex)
            })
            .ToList();
    }

    private static void NavigateFromSearchSuggestion(PersonalOverviewSearchSuggestion suggestion)
    {
        if (!string.IsNullOrWhiteSpace(suggestion.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(suggestion.InstanceId);
        }
    }

    private void OpenBusiestInboxButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_quickActionInstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(_quickActionInstanceId);
        }
    }

    private void RecentActivityList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PersonalOverviewActivityViewItem item)
        {
            ShellNavigationService.Instance.RequestInstance(item.Alert.InstanceId);
        }
    }

    private void InstanceTilesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PersonalOverviewTileViewItem tile)
        {
            ShellNavigationService.Instance.RequestInstance(tile.InstanceId);
        }
    }

    private sealed class PersonalOverviewActivityViewItem
    {
        public required NotificationAlert Alert { get; init; }

        public required string Title { get; init; }

        public required string Body { get; init; }

        public required string InstanceDisplayName { get; init; }

        public required string RelativeTimeText { get; init; }

        public required string IconGlyph { get; init; }

        public required SolidColorBrush AccentBrush { get; init; }

        public Visibility UnreadIndicatorVisibility { get; init; }
    }

    private sealed class PersonalOverviewTileViewItem
    {
        public required string InstanceId { get; init; }

        public required string DisplayName { get; init; }

        public required string PlatformLabel { get; init; }

        public required string DetailLine { get; init; }

        public required string ConnectionStatusLabel { get; init; }

        public required SolidColorBrush ConnectionBrush { get; init; }

        public required string IconGlyph { get; init; }

        public required SolidColorBrush AccentBrush { get; init; }

        public Visibility MutedIndicatorVisibility { get; init; }

        public Visibility UnreadBadgeVisibility { get; init; }

        public string UnreadBadgeText { get; init; } = string.Empty;
    }

    private sealed class PersonalOverviewSearchSuggestion
    {
        public required string Label { get; init; }

        public required string SubLabel { get; init; }

        public string? InstanceId { get; init; }

        public SolidColorBrush? AccentBrush { get; init; }

        public override string ToString() => $"{Label} ({SubLabel})";
    }
}
