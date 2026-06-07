using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;
using Windows.UI;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter : UserControl
{
    private const int RefreshDebounceMilliseconds = 450;

    private readonly ObservableCollection<BranchMetricViewModel> _branchMetrics = [];
    private readonly ObservableCollection<PlatformHealthViewModel> _platformHealth = [];
    private readonly ObservableCollection<OperationsThreadCardViewModel> _immediateQueue = [];
    private readonly ObservableCollection<OperationsThreadCardViewModel> _newInquiries = [];
    private readonly ObservableCollection<OperationsThreadCardViewModel> _hangingLeads = [];
    private readonly ObservableCollection<OperationsThreadCardViewModel> _resolved = [];
    private readonly ObservableCollection<HealthChipViewModel> _healthChips = [];

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _refreshDebounceTimer;

    private IEnumerable<MessengerInstance> _professionalInstances = [];
    private OperationsCommandCenterSnapshot _snapshot = OperationsCommandCenterSnapshot.Empty;
    private InstanceRegistryService? _registry;
    private string? _selectedBranchKey;
    private string? _selectedKanbanBranchName;
    private int _refreshGeneration;
    private bool _suppressTabSelection;
    private bool _isRefreshing;
    private GoogleReviewAlertView? _selectedReviewAlert;

    public OperationsCommandCenter()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        BranchMetricsList.ItemsSource = _branchMetrics;
        PlatformHealthItems.ItemsSource = _platformHealth;
        ImmediateQueueList.ItemsSource = _immediateQueue;
        NewInquiriesList.ItemsSource = _newInquiries;
        HangingLeadsList.ItemsSource = _hangingLeads;
        ResolvedList.ItemsSource = _resolved;
        HealthChipsItems.ItemsSource = _healthChips;

        _refreshDebounceTimer = _dispatcherQueue.CreateTimer();
        _refreshDebounceTimer.Interval = TimeSpan.FromMilliseconds(RefreshDebounceMilliseconds);
        _refreshDebounceTimer.Tick += OnRefreshDebounceTick;

        BranchWorkspaceTabs.RegisterPropertyChangedCallback(
            TabView.SelectedIndexProperty,
            OnBranchWorkspaceSelectedIndexChanged);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public async Task RefreshAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey = null,
        InstanceRegistryService? registry = null)
    {
        _professionalInstances = professionalInstances;
        var branchFilterChanged = !string.Equals(
            _selectedBranchKey,
            selectedBranchKey,
            StringComparison.OrdinalIgnoreCase);
        _selectedBranchKey = selectedBranchKey;
        _registry = registry;

        if (branchFilterChanged && !string.IsNullOrWhiteSpace(selectedBranchKey))
        {
            _selectedKanbanBranchName = selectedBranchKey.Trim();
        }

        if (_isRefreshing)
        {
            ScheduleDebouncedRefresh();
            return;
        }

        _isRefreshing = true;
        try
        {
            var snapshot = await Task.Run(() =>
                    OperationsCommandCenterService.Instance.BuildSnapshot(
                        professionalInstances,
                        selectedBranchKey))
                .ConfigureAwait(true);

            ApplySnapshot(snapshot);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public async Task RequestPlatformDataRefreshAsync(bool refreshAllInstances = false)
    {
        var targets = refreshAllInstances
            ? _professionalInstances.Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable).ToList()
            : _snapshot.FilteredInstances
                .Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable)
                .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        try
        {
            await DashboardScrapeOrchestrator.Instance
                .RefreshProfessionalInstancesAsync(targets)
                .ConfigureAwait(true);

            MessageAnalyticsService.Instance.NotifyDashboardRefresh();
            await RefreshAsync(_professionalInstances, _selectedBranchKey, _registry).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Platform data refresh failed: {ex.Message}");
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MessageTriageService.Instance.Changed += OnOperationalDataChanged;
        ThreadRegistryService.Instance.Changed += OnOperationalDataChanged;
        UnifiedMessengerDashboardService.Instance.Changed += OnOperationalDataChanged;
        MessageAnalyticsService.Instance.Changed += OnOperationalDataChanged;
        ProfessionalWorkspaceService.Instance.Changed += OnOperationalDataChanged;
        BackfillSyncManager.Instance.ProgressChanged += OnOperationalDataChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        MessageTriageService.Instance.Changed -= OnOperationalDataChanged;
        ThreadRegistryService.Instance.Changed -= OnOperationalDataChanged;
        UnifiedMessengerDashboardService.Instance.Changed -= OnOperationalDataChanged;
        MessageAnalyticsService.Instance.Changed -= OnOperationalDataChanged;
        ProfessionalWorkspaceService.Instance.Changed -= OnOperationalDataChanged;
        BackfillSyncManager.Instance.ProgressChanged -= OnOperationalDataChanged;
        _refreshDebounceTimer.Stop();
    }

    private void OnOperationalDataChanged(object? sender, EventArgs e) =>
        ScheduleDebouncedRefresh();

    private void ScheduleDebouncedRefresh()
    {
        Interlocked.Increment(ref _refreshGeneration);
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private void OnRefreshDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _ = RefreshAsync(_professionalInstances, _selectedBranchKey, _registry);
    }

    private void OnBranchWorkspaceSelectedIndexChanged(DependencyObject sender, DependencyProperty property)
    {
        if (_suppressTabSelection)
        {
            return;
        }

        _selectedKanbanBranchName = ResolveSelectedKanbanBranchName();
        ApplyKanbanForSelectedBranch();
        RefreshBranchMetricSelection();
    }

    private void ApplySnapshot(OperationsCommandCenterSnapshot snapshot)
    {
        _snapshot = snapshot;
        var threadOps = snapshot.ThreadOperations;
        var status = snapshot.Status;
        var hasProfessional = _professionalInstances.Any();

        EmptyStatePanel.Visibility = hasProfessional ? Visibility.Collapsed : Visibility.Visible;
        MainContentScrollViewer.Visibility = hasProfessional ? Visibility.Visible : Visibility.Collapsed;

        ScopeText.Text = snapshot.ScopeLabel;
        OpenThreadCountText.Text = status.OpenThreadCount.ToString();
        HangingLeadCountText.Text = status.HangingLeadCount.ToString();
        RevenueAtRiskText.Text = UnifiedMessengerDashboardPresentationHelper.FormatRevenue(status.TotalRevenueAtRisk);
        AvgReplyTimeValue.Text = status.AverageReplyTime;
        SlaBreachesValue.Text = status.SlaBreaches;
        ResponseRateValue.Text = status.ResponseRate;
        ImmediateActionCountText.Text = status.ImmediateActionCount.ToString();
        PeakHourValue.Text = status.PeakHour;
        DailyTrendValue.Text = status.DailyTrend;
        ApplySubtext(AvgReplyTimeSubtext, status.AverageReplyTimeSubtext);
        ApplySubtext(SlaThresholdSubtext, status.SlaThresholdSubtext);

        ReplaceCollection(
            _branchMetrics,
            threadOps.BranchMetrics.Select(metric =>
                new BranchMetricViewModel(metric, metric.BranchName.Equals(
                    _selectedKanbanBranchName,
                    StringComparison.OrdinalIgnoreCase))));
        BranchMetricsEmptyText.Visibility = _branchMetrics.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReplaceCollection(_platformHealth, status.PlatformHealth.Select(indicator => new PlatformHealthViewModel(indicator)));
        ReplaceCollection(_immediateQueue, threadOps.ImmediateActionQueue.Select(thread => new OperationsThreadCardViewModel(thread)));

        ImmediateQueueEmptyText.Visibility = _immediateQueue.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        RebuildBranchTabs(threadOps.BranchNames);
        ApplyKanbanForSelectedBranch();
        ApplyPlatformIntelligence(snapshot.PlatformIntelligence);
        ApplyAnalyticsTrends(snapshot.AnalyticsTrends, status);
        ApplyOperationalHighlights(snapshot.AnalyticsTrends.Highlights);
        ApplyAiInsightFeed(snapshot);
        ReplaceCollection(
            _healthChips,
            snapshot.InstanceHealthChips.Select(chip => new HealthChipViewModel(chip)));
        HealthChipsSection.Visibility = _healthChips.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static void ApplySubtext(TextBlock target, string text)
    {
        var visible = !string.IsNullOrWhiteSpace(text);
        target.Text = text;
        target.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyPlatformIntelligence(OperationsPlatformIntelligenceSnapshot platform)
    {
        var trustDisplay = platform.CustomerTrustDisplay;
        AggregateRatingValue.Text = trustDisplay.AggregateRating;
        UnrepliedReviewsValue.Text = trustDisplay.UnrepliedReviews;

        var reviewItems = trustDisplay.PendingReviews
            .Select(review => new GoogleReviewAlertView(review))
            .ToList();
        GoogleReviewAlertsList.ItemsSource = reviewItems;

        var hasReviews = reviewItems.Count > 0;
        var googleEmptyReason = DashboardCardEmptyStateHelper.ResolveGoogleTrustEmptyReason(
            platform.HasGoogleInstances,
            platform.CustomerTrust);
        GoogleReviewAlertsEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatGoogleTrustEmptyMessage(googleEmptyReason);
        GoogleReviewAlertsEmptyText.Visibility = !hasReviews && googleEmptyReason != DashboardCardEmptyReason.HasData
            ? Visibility.Visible
            : Visibility.Collapsed;
        GoogleReviewAlertsList.Visibility = hasReviews ? Visibility.Visible : Visibility.Collapsed;

        if (_selectedReviewAlert is not null &&
            reviewItems.All(item => item.AlertId != _selectedReviewAlert.AlertId))
        {
            _selectedReviewAlert = null;
        }

        var metaDisplay = platform.MetaResponseDisplay;
        MetaAverageResponseValue.Text = metaDisplay.AverageResponse;
        MetaEfficiencyRatingValue.Text = metaDisplay.EfficiencyRating;
        MetaSampleCountValue.Text = $"Samples: {metaDisplay.SampleCount}";
        MetaLastInboundValue.Text = $"Last inbound: {metaDisplay.LastInbound}";
        MetaLastReplyValue.Text = $"Last reply: {metaDisplay.LastReply}";
        ApplySubtext(MetaPendingResponseText, metaDisplay.PendingResponseLabel);

        var metaEmptyReason = DashboardCardEmptyStateHelper.ResolveMetaResponseEmptyReason(
            platform.HasMetaInstances,
            platform.MetaResponse);
        MetaResponseEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatMetaResponseEmptyMessage(metaEmptyReason);
        MetaResponseEmptyText.Visibility = metaEmptyReason == DashboardCardEmptyReason.HasData
            ? Visibility.Collapsed
            : Visibility.Visible;

        GoogleIntelligenceSection.Visibility = platform.HasGoogleInstances
            ? Visibility.Visible
            : Visibility.Collapsed;
        MetaIntelligenceSection.Visibility = platform.HasMetaInstances
            ? Visibility.Visible
            : Visibility.Collapsed;
        PlatformIntelligenceExpander.Visibility = platform.HasGoogleInstances || platform.HasMetaInstances
            ? Visibility.Visible
            : Visibility.Collapsed;

        var summaryParts = new List<string>();
        if (platform.HasGoogleInstances && platform.CustomerTrust.TotalUnrepliedReviews > 0)
        {
            summaryParts.Add($"{platform.CustomerTrust.TotalUnrepliedReviews} unreplied review(s)");
        }

        if (platform.HasMetaInstances && platform.MetaResponse.SampleCount > 0)
        {
            summaryParts.Add("Meta samples loaded");
        }

        PlatformIntelligenceExpander.Header = summaryParts.Count == 0
            ? "Platform intelligence"
            : $"Platform intelligence · {string.Join(" · ", summaryParts)}";
    }

    private void ApplyAnalyticsTrends(
        OperationsAnalyticsTrendSnapshot analytics,
        OperationsStatusSnapshot status)
    {
        WeeklyChart.SetSeries(analytics.WeeklyActivity);
        SentimentChart.SetSeries(analytics.Triage);
        SentCountValue.Text = status.HasMessageVolume ? analytics.SentCount.ToString() : "—";
        ReceivedCountValue.Text = status.HasMessageVolume ? analytics.ReceivedCount.ToString() : "—";

        var hasVolume = analytics.HasMessageVolume || status.HasMessageVolume;
        AnalyticsVolumeEmptyText.Visibility = hasVolume ? Visibility.Collapsed : Visibility.Visible;
        WeeklyChart.Visibility = hasVolume ? Visibility.Visible : Visibility.Collapsed;

        var triageTotal = analytics.Triage.PositiveCount +
                          analytics.Triage.NeutralCount +
                          analytics.Triage.NegativeCount;
        SentimentEmptyText.Visibility = triageTotal > 0 ? Visibility.Collapsed : Visibility.Visible;
        SentimentChart.Visibility = triageTotal > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyOperationalHighlights(IReadOnlyList<OperationalHighlightItem> highlights)
    {
        var items = highlights
            .Select(item => new OperationalHighlightViewModel(item))
            .ToList();
        OperationalHighlightsList.ItemsSource = items;
        var hasHighlights = items.Count > 0;
        OperationalHighlightsEmptyText.Visibility = hasHighlights
            ? Visibility.Collapsed
            : Visibility.Visible;
        OperationalHighlightsList.Visibility = hasHighlights
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyAiInsightFeed(OperationsCommandCenterSnapshot snapshot)
    {
        var items = snapshot.AiInsightFeed
            .Select(item => new OperationsInsightFeedViewModel(item))
            .ToList();
        AiInsightFeedList.ItemsSource = items;
        var hasItems = items.Count > 0;
        if (hasItems)
        {
            AiInsightFeedEmptyText.Visibility = Visibility.Collapsed;
            return;
        }

        var triage = snapshot.AnalyticsTrends.Triage;
        var emptyReason = DashboardCardEmptyStateHelper.ResolveExecutiveInsightsEmptyReason(
            AppSettingsService.Instance.Settings.EnableLocalAi,
            triage.TotalTriageCount,
            snapshot.AiInsightFeed.Count);
        AiInsightFeedEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatExecutiveInsightsEmptyMessage(emptyReason);
        AiInsightFeedEmptyText.Visibility = Visibility.Visible;
    }

    private void RebuildBranchTabs(IReadOnlyList<string> branchNames)
    {
        _suppressTabSelection = true;
        BranchWorkspaceTabs.TabItems.Clear();

        if (branchNames.Count == 0)
        {
            BranchWorkspaceTabs.TabItems.Add(new TabViewItem
            {
                Header = "General",
                IsClosable = false,
                Tag = "General"
            });
        }
        else
        {
            foreach (var branch in branchNames)
            {
                BranchWorkspaceTabs.TabItems.Add(new TabViewItem
                {
                    Header = branch,
                    IsClosable = false,
                    Tag = branch
                });
            }
        }

        var selectedBranch = _selectedKanbanBranchName ?? ResolveFilteredBranchName();
        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedBranch))
        {
            for (var index = 0; index < BranchWorkspaceTabs.TabItems.Count; index++)
            {
                if (BranchWorkspaceTabs.TabItems[index] is TabViewItem tab &&
                    tab.Tag is string branch &&
                    branch.Equals(selectedBranch, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        BranchWorkspaceTabs.SelectedIndex = selectedIndex;
        _selectedKanbanBranchName = ResolveSelectedKanbanBranchName();
        _suppressTabSelection = false;
    }

    private string? ResolveFilteredBranchName() =>
        string.IsNullOrWhiteSpace(_selectedBranchKey) ? null : _selectedBranchKey.Trim();

    private string? ResolveSelectedKanbanBranchName()
    {
        var index = BranchWorkspaceTabs.SelectedIndex;
        if (index < 0 || index >= BranchWorkspaceTabs.TabItems.Count)
        {
            return null;
        }

        return BranchWorkspaceTabs.TabItems[index] is TabViewItem tab
            ? tab.Tag as string
            : null;
    }

    private void ApplyKanbanForSelectedBranch()
    {
        var branchName = _selectedKanbanBranchName ?? ResolveSelectedKanbanBranchName();

        var threads = UnifiedMessengerDashboardPresentationHelper.FilterThreadsForBranch(
            _snapshot.ThreadOperations.AllThreads,
            branchName);

        ReplaceCollection(
            _newInquiries,
            threads.Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.NewInquiries)
                .Select(thread => new OperationsThreadCardViewModel(thread)));
        ReplaceCollection(
            _hangingLeads,
            threads.Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads)
                .Select(thread => new OperationsThreadCardViewModel(thread)));
        ReplaceCollection(
            _resolved,
            threads.Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.Resolved)
                .Select(thread => new OperationsThreadCardViewModel(thread)));

        NewInquiriesEmptyText.Visibility = _newInquiries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HangingLeadsEmptyText.Visibility = _hangingLeads.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResolvedEmptyText.Visibility = _resolved.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void BranchMetricsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BranchMetricViewModel metric)
        {
            return;
        }

        _selectedKanbanBranchName = metric.BranchName;
        SelectKanbanTabForBranch(metric.BranchName);
        ApplyKanbanForSelectedBranch();
        RefreshBranchMetricSelection();
    }

    private void SelectKanbanTabForBranch(string branchName)
    {
        _suppressTabSelection = true;
        for (var index = 0; index < BranchWorkspaceTabs.TabItems.Count; index++)
        {
            if (BranchWorkspaceTabs.TabItems[index] is TabViewItem tab &&
                tab.Tag is string branch &&
                branch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                BranchWorkspaceTabs.SelectedIndex = index;
                break;
            }
        }

        _suppressTabSelection = false;
    }

    private void RefreshBranchMetricSelection()
    {
        var selected = _selectedKanbanBranchName;
        for (var index = 0; index < _branchMetrics.Count; index++)
        {
            var metric = _branchMetrics[index];
            var isSelected = !string.IsNullOrWhiteSpace(selected) &&
                             metric.BranchName.Equals(selected, StringComparison.OrdinalIgnoreCase);
            if (metric.IsSelected != isSelected)
            {
                _branchMetrics[index] = new BranchMetricViewModel(metric.Source, isSelected);
            }
        }
    }

    private void ThreadCardList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationsThreadCardViewModel card &&
            !string.IsNullOrWhiteSpace(card.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(card.InstanceId);
        }
    }

    private void InsightFeedList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not OperationsInsightFeedViewModel item)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(item.InstanceId);
        }
    }

    private void OperationalHighlightsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationalHighlightViewModel highlight &&
            !string.IsNullOrWhiteSpace(highlight.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(highlight.InstanceId);
        }
    }

    private void GoogleReviewAlertsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedReviewAlert = GoogleReviewAlertsList.SelectedItem as GoogleReviewAlertView;
        if (_selectedReviewAlert is not null && string.IsNullOrWhiteSpace(ReviewReplyBox.Text))
        {
            ReviewReplyBox.Text = $"Hi {_selectedReviewAlert.ReviewerName}, thank you for your feedback. ";
        }
    }

    private async void SubmitReviewReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedReviewAlert is null)
        {
            return;
        }

        var replyText = ReviewReplyBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(replyText))
        {
            return;
        }

        ShellNavigationService.Instance.RequestInstance(_selectedReviewAlert.InstanceId);

        var reviewId = JsonSerializer.Serialize(_selectedReviewAlert.ReviewId);
        var reply = JsonSerializer.Serialize(replyText);
        var script = $"window.__umSubmitReviewReply({reviewId}, {reply});";

        await InstanceSessionManager.Instance.ExecuteScriptOnInstanceAsync(
            _selectedReviewAlert.InstanceId,
            script);
    }

    private void OpenSelectedReviewInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        var instanceId = _selectedReviewAlert?.InstanceId ??
            _snapshot.PlatformIntelligence.GoogleInstanceIds.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            ShellNavigationService.Instance.RequestInstance(instanceId);
        }
    }

    private async void RefreshPlatformDataButton_Click(object sender, RoutedEventArgs e) =>
        await RequestPlatformDataRefreshAsync().ConfigureAwait(true);

    private async void ExportAnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            return;
        }

        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = $"unified-messenger-analytics-{DateTime.Now:yyyyMMdd}";
        picker.FileTypeChoices.Add("JSON", [".json"]);
        picker.FileTypeChoices.Add("CSV", [".csv"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            if (file.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                await MessageAnalyticsService.Instance.ExportCsvAsync(_registry.Instances, file.Path);
            }
            else
            {
                await MessageAnalyticsService.Instance.ExportToFileAsync(file.Path);
            }

            var dialog = new ContentDialog
            {
                Title = "Export complete",
                Content = $"Analytics saved to {file.Name}.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Export failed",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private sealed class OperationsInsightFeedViewModel(OperationsInsightFeedItem item)
    {
        public string CustomerName { get; } = item.CustomerName;

        public string BranchName { get; } = item.BranchName;

        public string Summary { get; } = item.Summary;

        public string SourceLabel { get; } = item.SourceLabel;

        public string IntentLabel { get; } = item.IntentLabel;

        public string UrgencyLabel { get; } = item.UrgencyLabel;

        public string? InstanceId { get; } = item.InstanceId;

        public Visibility IntentLabelVisibility { get; } = string.IsNullOrWhiteSpace(item.IntentLabel)
            ? Visibility.Collapsed
            : Visibility.Visible;

        public Visibility UrgencyLabelVisibility { get; } = string.IsNullOrWhiteSpace(item.UrgencyLabel)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private sealed class BranchMetricViewModel
    {
        public BranchMetricViewModel(UnifiedMessengerBranchMetrics metric, bool isSelected)
        {
            Source = metric;
            BranchName = metric.BranchName;
            LatencyDisplay = UnifiedMessengerDashboardPresentationHelper.FormatLatency(metric.AverageLatencyMinutes);
            UnresolvedDisplay = metric.UnresolvedCount == 1
                ? "1 open"
                : $"{metric.UnresolvedCount} open";
            InboxDisplay = metric.InboxCount <= 1
                ? "1 inbox"
                : $"{metric.InboxCount} inboxes";
            PlatformBreakdown = metric.PlatformBreakdown;
            DetailDisplay = BuildDetailDisplay(metric);
            IsSelected = isSelected;
            LatencyBrush = CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));
            LatencyBorderBrush = CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));
            CardBackgroundBrush = isSelected
                ? new SolidColorBrush(Color.FromArgb(255, 239, 246, 255))
                : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }

        public UnifiedMessengerBranchMetrics Source { get; }

        public string BranchName { get; }

        public string LatencyDisplay { get; }

        public string UnresolvedDisplay { get; }

        public string InboxDisplay { get; }

        public string PlatformBreakdown { get; }

        public string DetailDisplay { get; }

        public bool IsSelected { get; }

        public SolidColorBrush LatencyBrush { get; }

        public SolidColorBrush LatencyBorderBrush { get; }

        public SolidColorBrush CardBackgroundBrush { get; }

        public Visibility PlatformBreakdownVisibility =>
            string.IsNullOrWhiteSpace(PlatformBreakdown) ? Visibility.Collapsed : Visibility.Visible;

        private static string BuildDetailDisplay(UnifiedMessengerBranchMetrics metric)
        {
            var parts = new List<string> { metric.UnresolvedCount == 1 ? "1 open" : $"{metric.UnresolvedCount} open" };
            if (metric.InboxCount > 0)
            {
                parts.Add(metric.InboxCount == 1 ? "1 inbox" : $"{metric.InboxCount} inboxes");
            }

            if (metric.SlaBreachCount > 0)
            {
                parts.Add($"{metric.SlaBreachCount} SLA");
            }

            if (metric.RevenueAtRisk > 0)
            {
                parts.Add(UnifiedMessengerDashboardPresentationHelper.FormatRevenue(metric.RevenueAtRisk));
            }

            return string.Join(" · ", parts);
        }
    }

    private sealed class PlatformHealthViewModel(UnifiedMessengerPlatformHealthIndicator indicator)
    {
        public string Label { get; } = $"{indicator.DisplayName}: {indicator.StatusText}";

        public SolidColorBrush StatusBrush { get; } = new(indicator.IsSynced
            ? Color.FromArgb(255, 34, 197, 94)
            : Color.FromArgb(255, 239, 68, 68));
    }

    private sealed class HealthChipViewModel(DashboardInstanceHealthChip chip)
    {
        public string Summary { get; } = string.IsNullOrWhiteSpace(chip.BackfillSummary)
            ? $"{chip.DisplayName}: backfill {chip.BackfillState}, {chip.AdapterHealth}, {chip.TriageItemCount} triage"
            : $"{chip.DisplayName}: backfill {chip.BackfillState}, {chip.AdapterHealth}, {chip.TriageItemCount} triage · {chip.BackfillSummary}";
    }

    private sealed class OperationalHighlightViewModel(OperationalHighlightItem item)
    {
        public string Title { get; } = item.Title;

        public string Subtitle { get; } = item.Subtitle;

        public string InstanceDisplayName { get; } = item.InstanceDisplayName;

        public string? InstanceId { get; } = item.InstanceId;
    }

    private sealed class GoogleReviewAlertView
    {
        public GoogleReviewAlertView(GoogleReviewAlert alert)
        {
            AlertId = alert.Id;
            InstanceId = alert.InstanceId;
            ReviewId = alert.ReviewId;
            ReviewerName = alert.ReviewerName;
            Snippet = alert.Snippet;
        }

        public string AlertId { get; }

        public string InstanceId { get; }

        public string ReviewId { get; }

        public string ReviewerName { get; }

        public string Snippet { get; }
    }

    private static SolidColorBrush CreateBrush(string hex) =>
        new(ColorFromHex(hex));

    private static Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
        {
            return Colors.Gray;
        }

        return Color.FromArgb(
            255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}
