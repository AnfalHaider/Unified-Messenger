using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        RecentActivityList.ItemsSource = _viewModel.ActivityItems;
        InstanceTilesList.ItemsSource = _viewModel.TileItems;
        GlobalSearchBox.ItemsSource = _viewModel.SearchSuggestions;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public PersonalOverviewViewModel ViewModel => _viewModel;

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
            WireResponsiveLayoutHelpers(rootScrollViewer);
        }

        ApplyPersonalLayoutPreferences();
    }

    private void WireResponsiveLayoutHelpers(ScrollViewer rootScrollViewer)
    {
        if (VisualStateManager.GetVisualStateGroups(SummaryCardsGrid) is { Count: > 0 } summaryGroups)
        {
            ScrollOffsetPreservationHelper.Attach(summaryGroups[0], rootScrollViewer);
        }

        if (VisualStateManager.GetVisualStateGroups(ContentGrid) is { Count: > 0 } contentGroups)
        {
            ScrollOffsetPreservationHelper.Attach(contentGroups[0], rootScrollViewer);
        }
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
                _services.PersonalDashboard.BuildSnapshot(
                    instances,
                    _services.NotificationHub,
                    _services.SessionManager,
                    _services.ResourceMonitor,
                    _services.AdapterHealth,
                    _services.ConnectionStatus))
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

        RecentActivityEmptyTitle.Text = _viewModel.ActivityEmptyTitle;
        RecentActivityEmptyText.Text = _viewModel.ActivityEmptyHint;
        RecentActivityEmptyIcon.Glyph = _viewModel.ActivityEmptyIconGlyph;
        RecentActivityEmptyPanel.Visibility = _viewModel.ShowActivityEmptyState
            ? Visibility.Visible
            : Visibility.Collapsed;
        RecentActivityList.Visibility = _viewModel.ShowActivityList
            ? Visibility.Visible
            : Visibility.Collapsed;

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
        if (args.ChosenSuggestion is PersonalOverviewSearchSuggestionViewModel suggestion)
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
        if (args.SelectedItem is PersonalOverviewSearchSuggestionViewModel suggestion)
        {
            NavigateFromSearchSuggestion(suggestion.InstanceId);
        }
    }

    private void UpdateSearchSuggestions(string? query)
    {
        var suggestions = BuildSearchSuggestions(query);
        _viewModel.ApplySearchSuggestions(suggestions);
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
        if (e.ClickedItem is PersonalOverviewActivityRowViewModel item)
        {
            NotificationNavigationHelper.OpenAlert(_services.Navigation, item.Alert);
        }
    }

    private void InstanceTilesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PersonalOverviewTileRowViewModel tile)
        {
            _services.Navigation.OpenInstance(tile.InstanceId);
        }
    }
}
