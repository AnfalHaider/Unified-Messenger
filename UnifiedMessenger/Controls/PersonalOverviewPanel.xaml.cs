using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class PersonalOverviewPanel : UserControl
{
    private const int RefreshDebounceMilliseconds = 300;

    private const int SearchDebounceMilliseconds = 250;

    private readonly PersonalOverviewViewModel _viewModel = new();

    private ApplicationServices _services = new();

    private IEnumerable<MessengerInstance> _personalInstances = [];
    private DispatcherQueueTimer? _refreshDebounceTimer;
    private DispatcherQueueTimer? _searchDebounceTimer;

    public PersonalOverviewPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public void ApplyAccessibilityTabOrder() =>
        AccessibilityTabOrderHelper.ApplyTabIndex(
            GlobalSearchBox,
            AccessibilityTabOrderHelper.PersonalSearchBox);

    public void ToggleLayoutEditMode() => SetPersonalLayoutEditMode(!_isLayoutEditMode);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Content is ScrollViewer rootScrollViewer)
        {
            ScrollInputHelper.EnableVerticalScrollBubbling(SummaryCardsGrid, rootScrollViewer);
        }

        ApplyPersonalLayoutPreferences();
    }

    public void Refresh(IEnumerable<MessengerInstance> personalInstances)
    {
        _personalInstances = personalInstances;
        _ = RefreshSnapshotAsync(GlobalSearchBox.Text);
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

        if (_searchDebounceTimer is not null)
        {
            _searchDebounceTimer.Tick -= OnSearchDebounceTick;
            _searchDebounceTimer.Stop();
            _searchDebounceTimer = null;
        }
    }

    private void OnRefreshDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _ = RefreshSnapshotAsync(GlobalSearchBox.Text);
    }

    private void OnSearchDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _ = RefreshSnapshotAsync(GlobalSearchBox.Text);
    }

    private async Task RefreshSnapshotAsync(string? searchQuery)
    {
        var instances = _personalInstances.ToList();
        var snapshot = await Task.Run(() =>
                PersonalDashboardService.Instance.BuildSnapshot(
                    instances,
                    _services.NotificationHub,
                    _services.SessionManager,
                    ResourceMonitorService.Instance,
                    AdapterHealthMonitor.Instance,
                    InstanceConnectionStatusService.Instance))
            .ConfigureAwait(true);

        ApplySnapshot(snapshot, searchQuery);
    }

    private void ApplySnapshot(PersonalDashboardSnapshot snapshot, string? searchQuery)
    {
        var viewState = PersonalSnapshotPresenter.BuildViewState(snapshot, searchQuery);
        _viewModel.ApplyViewState(viewState);
        SyncUiFromViewModel();
        UpdateSearchSuggestions(searchQuery);
    }

    private void SyncUiFromViewModel()
    {
        SummaryCardAccounts.Value = _viewModel.PersonalAccountCount.ToString();
        SummaryCardUnread.Value = _viewModel.TotalUnreadCount.ToString();
        SummaryCardMemory.Value = $"{_viewModel.AppWorkingSetMegabytes} MB";
        SummaryCardActive.Value = _viewModel.VisibleInstanceName;
        PersonalLastUpdatedText.Text = _viewModel.LastUpdatedText;

        OpenBusiestInboxButton.Content = _viewModel.QuickActionLabel;
        OpenBusiestInboxButton.Visibility = _viewModel.ShowQuickAction
            ? Visibility.Visible
            : Visibility.Collapsed;

        RecentActivityList.ItemsSource = _viewModel.ActivityItems
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

        RecentActivityEmptyTitle.Text = _viewModel.ActivityEmptyTitle;
        RecentActivityEmptyText.Text = _viewModel.ActivityEmptyHint;
        RecentActivityEmptyIcon.Glyph = _viewModel.ActivityEmptyIconGlyph;
        RecentActivityEmptyPanel.Visibility = _viewModel.ShowActivityEmptyState
            ? Visibility.Visible
            : Visibility.Collapsed;
        RecentActivityList.Visibility = _viewModel.ShowActivityList
            ? Visibility.Visible
            : Visibility.Collapsed;

        InstanceTilesList.ItemsSource = _viewModel.TileItems
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
                UnreadBadgeVisibility = tile.ShowUnreadBadge ? Visibility.Visible : Visibility.Collapsed,
                UnreadBadgeText = tile.UnreadBadgeText
            })
            .ToList();

        InstanceTilesEmptyText.Text = _viewModel.InstanceTilesEmptyHint;
        InstanceTilesEmptyPanel.Visibility = _viewModel.ShowInstanceTilesEmptyState
            ? Visibility.Visible
            : Visibility.Collapsed;
        InstanceTilesList.Visibility = _viewModel.ShowInstanceTilesEmptyState
            ? Visibility.Collapsed
            : Visibility.Visible;

        NoAccountsEmptyPanel.Visibility = _viewModel.ShowNoAccountsEmptyState
            ? Visibility.Visible
            : Visibility.Collapsed;
        GlobalSearchBox.Visibility = _viewModel.ShowToolbar ? Visibility.Visible : Visibility.Collapsed;
        SummaryCardsGrid.Visibility = _viewModel.ShowToolbar ? Visibility.Visible : Visibility.Collapsed;
        PersonalToolbarGrid.Visibility = _viewModel.ShowToolbar ? Visibility.Visible : Visibility.Collapsed;
        ContentGrid.Visibility = _viewModel.ShowContent ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddPersonalAccountButton_Click(object sender, RoutedEventArgs e) =>
        _services.Navigation.RequestAddInstance();

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

        ScheduleSearchRefresh();
    }

    private void ScheduleSearchRefresh()
    {
        _searchDebounceTimer ??= DispatcherQueue.CreateTimer();
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(SearchDebounceMilliseconds);
        _searchDebounceTimer.Tick -= OnSearchDebounceTick;
        _searchDebounceTimer.Tick += OnSearchDebounceTick;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is PersonalOverviewSearchSuggestion suggestion)
        {
            NavigateFromSearchSuggestion(suggestion.InstanceId);
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
            NavigateFromSearchSuggestion(suggestions[0].InstanceId);
        }
    }

    private void GlobalSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is PersonalOverviewSearchSuggestion suggestion)
        {
            NavigateFromSearchSuggestion(suggestion.InstanceId);
        }
    }

    private void UpdateSearchSuggestions(string? query)
    {
        var suggestions = BuildSearchSuggestions(query);
        _viewModel.ApplySearchSuggestions(suggestions);
        GlobalSearchBox.ItemsSource = suggestions
            .Select(suggestion => new PersonalOverviewSearchSuggestion
            {
                Label = suggestion.Label,
                SubLabel = suggestion.SubLabel,
                InstanceId = suggestion.InstanceId,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(suggestion.AccentColorHex)
            })
            .ToList();
    }

    private List<PersonalOverviewSearchSuggestionViewModel> BuildSearchSuggestions(string? query)
    {
        var personalAlerts = _services.NotificationHub.Alerts
            .Where(alert => PersonalInstanceIds.Contains(alert.InstanceId));

        return PersonalOverviewSearchPresenter
            .BuildSuggestions(_personalInstances, personalAlerts, query)
            .ToList();
    }

    private void NavigateFromSearchSuggestion(string? instanceId)
    {
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            _services.Navigation.OpenInstance(instanceId);
        }
    }

    private void OpenBusiestInboxButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.QuickActionInstanceId))
        {
            _services.Navigation.OpenInstance(_viewModel.QuickActionInstanceId);
        }
    }

    private void RecentActivityList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PersonalOverviewActivityViewItem item)
        {
            NotificationNavigationHelper.OpenAlert(_services.Navigation, item.Alert);
        }
    }

    private void InstanceTilesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PersonalOverviewTileViewItem tile)
        {
            _services.Navigation.OpenInstance(tile.InstanceId);
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
