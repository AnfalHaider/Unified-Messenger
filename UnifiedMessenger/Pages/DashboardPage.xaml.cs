using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Pages;

public sealed partial class DashboardPage : Page
{
    private InstanceRegistryService? _registry;
    private readonly List<DashboardActivityItem> _allActivity = [];
    private DispatcherTimer? _resourceTimer;
    private string? _selectedBranchKey;
    private bool _suppressBranchSelectionChanged;
    private readonly ObservableCollection<DashboardBranchFilterEntry> _branchFilterEntries = new();

    public ObservableCollection<DashboardBranchFilterEntry> BranchFilterEntries => _branchFilterEntries;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnResourceTimerTick(object? sender, object e)
    {
        RefreshResources();
        _ = RefreshOperationsCommandCenterAsync();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is RegistryNavigationArgs args)
        {
            _registry = args.Registry;
        }

        RefreshAll();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NotificationHub.Instance.Changed += OnHubChanged;
        AdapterHealthMonitor.Instance.Changed += OnAdapterHealthChanged;
        MessageAnalyticsService.Instance.Changed += OnAnalyticsChanged;
        MessageTriageService.Instance.Changed += OnTriageChanged;
        ProfessionalWorkspaceService.Instance.Changed += OnProfessionalWorkspaceChanged;
        ThreadRegistryService.Instance.Changed += OnThreadRegistryChanged;
        BackfillSyncManager.Instance.ProgressChanged += OnBackfillProgressChanged;

        _resourceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(DashboardPageHelper.ResourceRefreshIntervalSeconds)
        };
        _resourceTimer.Tick += OnResourceTimerTick;
        _resourceTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        NotificationHub.Instance.Changed -= OnHubChanged;
        AdapterHealthMonitor.Instance.Changed -= OnAdapterHealthChanged;
        MessageAnalyticsService.Instance.Changed -= OnAnalyticsChanged;
        MessageTriageService.Instance.Changed -= OnTriageChanged;
        ProfessionalWorkspaceService.Instance.Changed -= OnProfessionalWorkspaceChanged;
        ThreadRegistryService.Instance.Changed -= OnThreadRegistryChanged;
        BackfillSyncManager.Instance.ProgressChanged -= OnBackfillProgressChanged;

        if (_resourceTimer is not null)
        {
            _resourceTimer.Tick -= OnResourceTimerTick;
            _resourceTimer.Stop();
            _resourceTimer = null;
        }
    }

    private void OnHubChanged(object? sender, NotificationHubChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshActivity();
            _ = RefreshOperationsCommandCenterAsync();
        });
    }

    private void OnAdapterHealthChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshResources);
    }

    private void OnAnalyticsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = RefreshOperationsCommandCenterAsync());

    private void OnTriageChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = RefreshOperationsCommandCenterAsync());

    private void OnProfessionalWorkspaceChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = RefreshOperationsCommandCenterAsync());

    private void OnThreadRegistryChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = RefreshOperationsCommandCenterAsync());

    private void OnBackfillProgressChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = RefreshOperationsCommandCenterAsync());

    public void RefreshAll()
    {
        if (_registry is null)
        {
            WelcomeSubtitle.Text = "Add an account to start receiving unified notifications.";
            return;
        }

        var professionalCount = _registry.Instances.Count(i => i.IsProfessional);
        var personalCount = _registry.Instances.Count - professionalCount;

        WelcomeSubtitle.Text = DashboardPageHelper.BuildWelcomeSubtitle(professionalCount, personalCount);

        RefreshBranchFilter();
        RefreshActivity();
        RefreshResources();
        UpdateSearchSuggestions(GlobalSearchBox.Text);
        _ = RefreshOperationsCommandCenterAsync();
    }

    private async Task RefreshOperationsCommandCenterAsync()
    {
        if (_registry is null)
        {
            return;
        }

        await OperationsCommandCenterPanel.RefreshAsync(
            ProfessionalInstances,
            _selectedBranchKey,
            _registry).ConfigureAwait(true);
    }

    private async void RefreshDashboardDataButton_Click(object sender, RoutedEventArgs e) =>
        await RequestProfessionalTelemetryRefreshAsync();

    private async Task RequestProfessionalTelemetryRefreshAsync(bool refreshAllInstances = false)
    {
        if (_registry is null)
        {
            return;
        }

        RefreshDashboardDataButton.IsEnabled = false;
        try
        {
            ScheduleBackfillRetryIfNeeded();
            await OperationsCommandCenterPanel
                .RequestPlatformDataRefreshAsync(refreshAllInstances)
                .ConfigureAwait(true);
        }
        finally
        {
            RefreshDashboardDataButton.IsEnabled = true;
        }
    }

    private void ScheduleBackfillRetryIfNeeded()
    {
        if (!AppSettingsService.Instance.Settings.EnableStartupBackfill)
        {
            return;
        }

        foreach (var instance in ProfessionalInstances)
        {
            var state = BackfillSyncManager.Instance.GetState(instance.Id);
            if (state is BackfillSyncState.NotStarted or BackfillSyncState.Failed or BackfillSyncState.Skipped)
            {
                BackfillSyncManager.Instance.Schedule(instance);
            }
        }
    }

    private IEnumerable<MessengerInstance> ProfessionalInstances =>
        _registry?.Instances.Where(i => i.IsProfessional) ?? [];

    private void RefreshBranchFilter()
    {
        if (_registry is null)
        {
            _branchFilterEntries.Clear();
            return;
        }

        _suppressBranchSelectionChanged = true;
        _branchFilterEntries.Clear();
        foreach (var entry in BranchWorkspaceHelper.BuildBranchFilterEntries(ProfessionalInstances))
        {
            _branchFilterEntries.Add(entry);
        }

        BranchFilterBox.SelectedItem = _branchFilterEntries.FirstOrDefault(entry =>
            entry.IsAllBranches && string.IsNullOrWhiteSpace(_selectedBranchKey) ||
            !entry.IsAllBranches &&
            entry.BranchKey.Equals(_selectedBranchKey ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (BranchFilterBox.SelectedItem is null && _branchFilterEntries.Count > 0)
        {
            BranchFilterBox.SelectedIndex = 0;
            _selectedBranchKey = null;
        }

        _suppressBranchSelectionChanged = false;
    }

    private void BranchFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressBranchSelectionChanged ||
            BranchFilterBox.SelectedItem is not DashboardBranchFilterEntry entry)
        {
            return;
        }

        _selectedBranchKey = DashboardPageHelper.ResolveBranchInstanceId(entry);

        _ = RefreshOperationsCommandCenterAsync();
        _ = OperationsCommandCenterPanel.RequestPlatformDataRefreshAsync();
    }

    private IEnumerable<MessengerInstance> PersonalInstances =>
        _registry?.Instances.Where(i => !i.IsProfessional) ?? [];

    private void RefreshActivity()
    {
        _allActivity.Clear();
        var personalIds = PersonalInstances.Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var alert in NotificationHub.Instance.GetAlertsSortedByInstance())
        {
            if (!personalIds.Contains(alert.InstanceId))
            {
                continue;
            }

            var instance = _registry?.FindById(alert.InstanceId);
            _allActivity.Add(new DashboardActivityItem
            {
                Alert = alert,
                Title = alert.Title,
                Body = alert.Body,
                InstanceDisplayName = alert.InstanceDisplayName,
                RelativeTimeText = alert.RelativeTimeText,
                IconGlyph = instance?.IconGlyph ?? alert.IconGlyph,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(
                    instance?.AccentColor ?? PlatformBrandingHelper.DefaultAccentHex)
            });
        }

        ApplyActivityFilter(GlobalSearchBox.Text);
    }

    private void RefreshResources()
    {
        if (_registry is null)
        {
            return;
        }

        var personalList = PersonalInstances.ToList();
        var snapshot = ResourceMonitorService.Instance.Capture(
            personalList,
            InstanceSessionManager.Instance,
            NotificationHub.Instance,
            AdapterHealthMonitor.Instance);

        ActiveAccountsValue.Text = snapshot.ActiveAccountCount.ToString();
        MemoryValue.Text = $"{snapshot.WorkingSetMegabytes} MB";
        UnreadValue.Text = snapshot.TotalUnreadCount.ToString();
        VisibleAccountValue.Text = snapshot.VisibleInstanceName;

        InstanceTilesList.ItemsSource = snapshot.InstanceTiles
            .Select(tile => new DashboardInstanceTileItem
            {
                InstanceId = tile.InstanceId,
                DisplayName = tile.DisplayName,
                PlatformLabel = tile.Platform,
                StatusLine = DashboardPageHelper.BuildInstanceStatusLine(tile),
                IconGlyph = tile.IconGlyph,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(tile.AccentColor)
            })
            .ToList();
    }

    private void GlobalSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            return;
        }

        ApplyActivityFilter(sender.Text);
        UpdateSearchSuggestions(sender.Text);
    }

    private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is DashboardSearchSuggestion suggestion)
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
        if (args.SelectedItem is DashboardSearchSuggestion suggestion)
        {
            NavigateFromSearchSuggestion(suggestion);
        }
    }

    private void UpdateSearchSuggestions(string? query)
    {
        GlobalSearchBox.ItemsSource = BuildSearchSuggestions(query);
    }

    private List<DashboardSearchSuggestion> BuildSearchSuggestions(string? query)
    {
        if (_registry is null)
        {
            return [];
        }

        return DashboardPageHelper
            .FilterPersonalSearchMatches(PersonalInstances, query)
            .Select(match => new DashboardSearchSuggestion
            {
                Label = match.Label,
                SubLabel = match.SubLabel,
                InstanceId = match.InstanceId,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(match.AccentColorHex)
            })
            .ToList();
    }

    private static void NavigateFromSearchSuggestion(DashboardSearchSuggestion suggestion)
    {
        if (!string.IsNullOrWhiteSpace(suggestion.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(suggestion.InstanceId);
        }
    }

    private void ApplyActivityFilter(string? query)
    {
        IEnumerable<DashboardActivityItem> filtered = _allActivity;
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        if (hasQuery)
        {
            filtered = _allActivity.Where(item => item.Matches(query!));
        }

        var list = filtered.ToList();
        RecentActivityList.ItemsSource = list;

        if (list.Count == 0)
        {
            RecentActivityEmptyText.Text = DashboardPageHelper.ResolveEmptyActivityMessage(hasQuery);
            RecentActivityEmptyText.Visibility = Visibility.Visible;
            RecentActivityList.Visibility = Visibility.Collapsed;
        }
        else
        {
            RecentActivityEmptyText.Visibility = Visibility.Collapsed;
            RecentActivityList.Visibility = Visibility.Visible;
        }
    }

    private void RecentActivityList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DashboardActivityItem item)
        {
            ShellNavigationService.Instance.RequestInstance(item.Alert.InstanceId);
        }
    }

    private void InstanceTilesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DashboardInstanceTileItem tile)
        {
            ShellNavigationService.Instance.RequestInstance(tile.InstanceId);
        }
    }

    private sealed class DashboardActivityItem
    {
        public required NotificationAlert Alert { get; init; }

        public required string Title { get; init; }

        public required string Body { get; init; }

        public required string InstanceDisplayName { get; init; }

        public required string RelativeTimeText { get; init; }

        public required string IconGlyph { get; init; }

        public required SolidColorBrush AccentBrush { get; init; }

        public bool Matches(string query) =>
            DashboardPageHelper.ActivityMatches(Title, Body, InstanceDisplayName, query);
    }

    private sealed class DashboardInstanceTileItem
    {
        public required string InstanceId { get; init; }

        public required string DisplayName { get; init; }

        public required string PlatformLabel { get; init; }

        public required string StatusLine { get; init; }

        public required string IconGlyph { get; init; }

        public required SolidColorBrush AccentBrush { get; init; }
    }

    private sealed class DashboardSearchSuggestion
    {
        public required string Label { get; init; }

        public required string SubLabel { get; init; }

        public string? InstanceId { get; init; }

        public SolidColorBrush? AccentBrush { get; init; }

        public override string ToString() => $"{Label} ({SubLabel})";
    }
}
