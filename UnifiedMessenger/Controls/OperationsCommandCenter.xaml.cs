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
    /// <summary>Single workspace scope for the entire OCC dashboard (null = all branches).</summary>
    private string? _workspaceBranchKey;
    private IReadOnlyList<string> _availableBranchKeys = [];
    private bool _suppressPillSelection;
    private string? _lastPillBarSignature;
    private bool _isRefreshing;
    private bool _showWorkspaceLoading;
    private GoogleReviewAlertView? _selectedReviewAlert;
    private IReadOnlyDictionary<string, BranchWorkspaceHelper.BranchTabCounts> _branchTabCounts =
        new Dictionary<string, BranchWorkspaceHelper.BranchTabCounts>(StringComparer.OrdinalIgnoreCase);
    public OperationsCommandCenter()
    {
        InitializeComponent();
        PlatformIntelligenceExpander.Collapsed += OnPlatformIntelligenceCollapsed;
        AnalyticsTrendsExpander.Collapsed += OnAnalyticsTrendsCollapsed;
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

        BranchWorkspacePillBar.SelectionChanged += OnBranchWorkspacePillSelectionChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public async Task RefreshAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        InstanceRegistryService? registry = null)
    {
        _professionalInstances = professionalInstances;
        _registry = registry;

        if (_isRefreshing)
        {
            ScheduleDebouncedRefresh();
            return;
        }

        _isRefreshing = true;
        if (_showWorkspaceLoading)
        {
            SetWorkspaceLoadingVisible(true);
        }

        try
        {
            var instanceList = professionalInstances.ToList();
            var allowedIds = instanceList
                .Select(instance => instance.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _availableBranchKeys = BranchWorkspaceHelper.CollectBranchKeys(
                instanceList,
                ThreadRegistryService.Instance.GetAllThreads()
                    .Where(thread => allowedIds.Contains(thread.InstanceId)));

            if (!string.IsNullOrWhiteSpace(_workspaceBranchKey) &&
                _availableBranchKeys.All(branch =>
                    !branch.Equals(_workspaceBranchKey, StringComparison.OrdinalIgnoreCase)))
            {
                _workspaceBranchKey = null;
            }

            var scopedThreads = ThreadRegistryService.Instance.GetAllThreads()
                .Where(thread => allowedIds.Contains(thread.InstanceId))
                .ToList();
            _branchTabCounts = BranchWorkspaceHelper.ComputeBranchTabCounts(scopedThreads);

            var snapshot = await Task.Run(() =>
                    OperationsCommandCenterService.Instance.BuildSnapshot(
                        instanceList,
                        _workspaceBranchKey))
                .ConfigureAwait(true);

            RebuildBranchPills(_availableBranchKeys);
            ApplySnapshot(snapshot);
        }
        finally
        {
            SetWorkspaceLoadingVisible(false);
            _showWorkspaceLoading = false;
            _isRefreshing = false;
        }
    }

    private void SetWorkspaceLoadingVisible(bool visible)
    {
        WorkspaceLoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BranchWorkspacePillBar.IsInteractionEnabled = !visible;
    }

    public async Task<bool> RequestPlatformDataRefreshAsync(bool refreshAllInstances = false)
    {
        var targets = refreshAllInstances
            ? _professionalInstances.Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable).ToList()
            : _snapshot.FilteredInstances
                .Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable)
                .ToList();

        if (targets.Count == 0)
        {
            await ShowSimpleDialogAsync(
                "Nothing to refresh",
                refreshAllInstances
                    ? "No professional accounts are configured for platform data scraping."
                    : "No accounts in the current workspace scope support platform data scraping.")
                .ConfigureAwait(true);
            return false;
        }

        try
        {
            await DashboardScrapeOrchestrator.Instance
                .RefreshProfessionalInstancesAsync(targets)
                .ConfigureAwait(true);

            MessageAnalyticsService.Instance.NotifyDashboardRefresh();
            await RefreshAsync(_professionalInstances, _registry).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync("Platform refresh failed", ex.Message).ConfigureAwait(true);
            return false;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WireScrollBubbling();
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
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private void OnRefreshDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _ = RefreshAsync(_professionalInstances, _registry);
    }

    private void OnBranchWorkspacePillSelectionChanged(object? sender, string? branchKey)
    {
        if (_suppressPillSelection)
        {
            return;
        }

        if (string.Equals(_workspaceBranchKey, branchKey, StringComparison.OrdinalIgnoreCase))
        {
            RefreshBranchMetricSelection();
            return;
        }

        _workspaceBranchKey = branchKey;
        _showWorkspaceLoading = true;
        _ = RefreshAsync(_professionalInstances, _registry);
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
        LastRefreshedText.Text = $"Updated {DateTime.Now:t}";
        ApplyStatusKpis(status);
        ApplyKanbanFromSnapshot(threadOps);
        ApplyImmediateQueueFromSnapshot(threadOps);

        ReplaceCollection(
            _branchMetrics,
            threadOps.BranchMetrics.Select(metric =>
                new BranchMetricViewModel(
                    metric,
                    IsWorkspaceBranchSelected(metric.BranchName),
                    IsWorkspaceBranchScoped())));
        BranchMetricsEmptyText.Visibility = _branchMetrics.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReplaceCollection(_platformHealth, status.PlatformHealth.Select(indicator => new PlatformHealthViewModel(indicator)));
        PlatformHealthSection.Visibility = _platformHealth.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyOperationalHighlights(snapshot.AnalyticsTrends.Highlights);
        ApplyPlatformIntelligence(snapshot.PlatformIntelligence);
        ApplyAnalyticsTrends(snapshot.AnalyticsTrends, status);
        ApplyAiInsightFeed(snapshot.AiInsightFeed);
        ReplaceCollection(
            _healthChips,
            snapshot.InstanceHealthChips.Select(chip => new HealthChipViewModel(chip)));
        HealthChipsSection.Visibility = _healthChips.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private bool IsWorkspaceBranchSelected(string branchName) =>
        !string.IsNullOrWhiteSpace(_workspaceBranchKey) &&
        branchName.Equals(_workspaceBranchKey, StringComparison.OrdinalIgnoreCase);

    private bool IsWorkspaceBranchScoped() =>
        !string.IsNullOrWhiteSpace(_workspaceBranchKey);

    private void ApplyKanbanFromSnapshot(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();

        ReplaceCollection(
            _newInquiries,
            threadOps.AllThreads
                .Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.NewInquiries)
                .Select(thread => new OperationsThreadCardViewModel(thread, hideBranch)));
        ReplaceCollection(
            _hangingLeads,
            threadOps.AllThreads
                .Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads)
                .Select(thread => new OperationsThreadCardViewModel(thread, hideBranch)));
        ReplaceCollection(
            _resolved,
            threadOps.AllThreads
                .Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.Resolved)
                .Select(thread => new OperationsThreadCardViewModel(thread, hideBranch)));

        NewInquiriesEmptyText.Visibility = _newInquiries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HangingLeadsEmptyText.Visibility = _hangingLeads.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResolvedEmptyText.Visibility = _resolved.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyImmediateQueueFromSnapshot(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();

        ReplaceCollection(
            _immediateQueue,
            threadOps.ImmediateActionQueue.Select(thread => new OperationsThreadCardViewModel(thread, hideBranch)));

        ImmediateQueueEmptyText.Visibility = _immediateQueue.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        var shownCount = threadOps.ImmediateActionQueueCount;
        var totalCount = threadOps.ImmediateActionTotal;
        if (totalCount > shownCount && shownCount > 0)
        {
            ImmediateQueueFooterText.Text =
                $"Showing top {shownCount} of {totalCount} urgent threads";
            ImmediateQueueFooterText.Visibility = Visibility.Visible;
        }
        else
        {
            ImmediateQueueFooterText.Visibility = Visibility.Collapsed;
        }
    }

    private bool ShouldHideBranchOnCards() =>
        !string.IsNullOrWhiteSpace(_workspaceBranchKey);

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

        PlatformIntelligenceHeaderText.Text = "Platform intelligence & refresh";

        if (platform.HasGoogleInstances && platform.CustomerTrust.TotalUnrepliedReviews > 0)
        {
            PlatformIntelligenceReviewBadge.Value = platform.CustomerTrust.TotalUnrepliedReviews;
            PlatformIntelligenceReviewBadge.Visibility = Visibility.Visible;
        }
        else
        {
            PlatformIntelligenceReviewBadge.Visibility = Visibility.Collapsed;
        }

        ApplyPlatformIntelligenceExpansion(platform);
    }

    private void OnPlatformIntelligenceCollapsed(Expander sender, ExpanderCollapsedEventArgs args)
    {
        var signal = DashboardCardEmptyStateHelper.ComputePlatformIntelligenceAlertSignal(
            _snapshot.PlatformIntelligence);
        if (signal > 0)
        {
            PersistOccDismissedSignal(
                signal,
                static (settings, value) =>
                    settings.OccPlatformIntelligenceDismissedSignal =
                        Math.Max(settings.OccPlatformIntelligenceDismissedSignal, value));
        }
    }

    private void OnAnalyticsTrendsCollapsed(Expander sender, ExpanderCollapsedEventArgs args)
    {
        var signal = DashboardCardEmptyStateHelper.ComputeAnalyticsTrendsAlertSignal(
            _snapshot.Status,
            _snapshot.AnalyticsTrends);
        if (signal > 0)
        {
            PersistOccDismissedSignal(
                signal,
                static (settings, value) =>
                    settings.OccAnalyticsTrendsDismissedSignal =
                        Math.Max(settings.OccAnalyticsTrendsDismissedSignal, value));
        }
    }

    private void ApplyPlatformIntelligenceExpansion(OperationsPlatformIntelligenceSnapshot platform)
    {
        var signal = DashboardCardEmptyStateHelper.ComputePlatformIntelligenceAlertSignal(platform);
        if (signal == 0)
        {
            ResetOccDismissedSignalIfNeeded(
                static settings => settings.OccPlatformIntelligenceDismissedSignal,
                static (settings, value) => settings.OccPlatformIntelligenceDismissedSignal = value);
            return;
        }

        var dismissed = AppSettingsService.Instance.Settings.OccPlatformIntelligenceDismissedSignal;

        if (DashboardCardEmptyStateHelper.ShouldAutoExpandPlatformIntelligence(platform) &&
            DashboardCardEmptyStateHelper.ShouldExpandOccSection(signal, dismissed))
        {
            PlatformIntelligenceExpander.IsExpanded = true;
        }
    }

    private void ApplyAnalyticsTrendsExpansion(
        OperationsAnalyticsTrendSnapshot analytics,
        OperationsStatusSnapshot status)
    {
        var signal = DashboardCardEmptyStateHelper.ComputeAnalyticsTrendsAlertSignal(status, analytics);
        if (signal == 0)
        {
            ResetOccDismissedSignalIfNeeded(
                static settings => settings.OccAnalyticsTrendsDismissedSignal,
                static (settings, value) => settings.OccAnalyticsTrendsDismissedSignal = value);
            return;
        }

        var dismissed = AppSettingsService.Instance.Settings.OccAnalyticsTrendsDismissedSignal;

        if (DashboardCardEmptyStateHelper.ShouldAutoExpandAnalyticsTrends(status, analytics) &&
            DashboardCardEmptyStateHelper.ShouldExpandOccSection(signal, dismissed))
        {
            AnalyticsTrendsExpander.IsExpanded = true;
        }
    }

    private static void PersistOccDismissedSignal(
        int signal,
        Action<AppSettings, int> assignDismissedSignal)
    {
        _ = AppSettingsService.Instance.UpdateAsync(settings => assignDismissedSignal(settings, signal));
    }

    private static void ResetOccDismissedSignalIfNeeded(
        Func<AppSettings, int> readDismissedSignal,
        Action<AppSettings, int> assignDismissedSignal)
    {
        if (readDismissedSignal(AppSettingsService.Instance.Settings) <= 0)
        {
            return;
        }

        _ = AppSettingsService.Instance.UpdateAsync(settings => assignDismissedSignal(settings, 0));
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

        AnalyticsTrendsSummaryText.Text = BuildAnalyticsTrendsSummary(status);
        ApplyAnalyticsTrendsExpansion(analytics, status);
    }

    private static string BuildAnalyticsTrendsSummary(OperationsStatusSnapshot status)
    {
        var parts = new List<string>();
        if (!string.Equals(status.AverageReplyTime, "—", StringComparison.Ordinal))
        {
            parts.Add($"Avg reply {status.AverageReplyTime}");
        }

        if (!string.Equals(status.ResponseRate, "—", StringComparison.Ordinal))
        {
            parts.Add($"Response {status.ResponseRate}");
        }

        if (!string.Equals(status.PeakHour, "—", StringComparison.Ordinal))
        {
            parts.Add($"Peak {status.PeakHour}");
        }

        if (!string.Equals(status.DailyTrend, "—", StringComparison.Ordinal))
        {
            parts.Add(status.DailyTrend);
        }

        return parts.Count == 0
            ? "Event analytics appear after message traffic is recorded."
            : string.Join(" · ", parts);
    }

    private void WireScrollBubbling()
    {
        ScrollInputHelper.EnableVerticalScrollBubbling(ImmediateQueueList, MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(NewInquiriesList, MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(HangingLeadsList, MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(ResolvedList, MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(OperationalHighlightsList, MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(AiInsightFeedList, MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(BranchMetricsScrollViewer, MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(BranchWorkspacePillBar, MainContentScrollViewer);
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

    private void ApplyAiInsightFeed(IReadOnlyList<OperationsInsightFeedItem> feed)
    {
        var items = feed
            .Select(item => new OperationsInsightFeedViewModel(item))
            .ToList();
        AiInsightFeedList.ItemsSource = items;
        var hasItems = items.Count > 0;
        if (hasItems)
        {
            AiInsightFeedEmptyText.Visibility = Visibility.Collapsed;
            return;
        }

        var triage = _snapshot.AnalyticsTrends.Triage;
        var emptyReason = DashboardCardEmptyStateHelper.ResolveExecutiveInsightsEmptyReason(
            AppSettingsService.Instance.Settings.EnableLocalAi,
            triage.TotalTriageCount,
            _snapshot.AiInsightFeed.Count);
        AiInsightFeedEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatExecutiveInsightsEmptyMessage(emptyReason);
        AiInsightFeedEmptyText.Visibility = Visibility.Visible;
    }

    private void RebuildBranchPills(IReadOnlyList<string> branchNames)
    {
        var signature = BuildPillBarSignature(branchNames);
        var pills = BuildBranchPillItems(branchNames);

        if (!string.Equals(signature, _lastPillBarSignature, StringComparison.Ordinal))
        {
            _lastPillBarSignature = signature;
            _suppressPillSelection = true;
            BranchWorkspacePillBar.SetItems(pills, _workspaceBranchKey);
            _suppressPillSelection = false;
            return;
        }

        _suppressPillSelection = true;
        BranchWorkspacePillBar.SelectBranchKey(_workspaceBranchKey);
        _suppressPillSelection = false;
    }

    private string BuildPillBarSignature(IReadOnlyList<string> branchNames)
    {
        var allBranchesCounts = BranchWorkspaceHelper.SumBranchTabCounts(_branchTabCounts);
        var parts = new List<string>
        {
            $"all:{allBranchesCounts.OpenCount}:{allBranchesCounts.ImmediateCount}"
        };

        foreach (var branch in branchNames)
        {
            var counts = _branchTabCounts.GetValueOrDefault(branch, new BranchWorkspaceHelper.BranchTabCounts(0, 0));
            parts.Add($"{branch}:{counts.OpenCount}:{counts.ImmediateCount}");
        }

        return string.Join("|", parts);
    }

    private IReadOnlyList<BranchWorkspacePillItem> BuildBranchPillItems(IReadOnlyList<string> branchNames)
    {
        var items = new List<BranchWorkspacePillItem>();
        var allBranchesCounts = BranchWorkspaceHelper.SumBranchTabCounts(_branchTabCounts);
        items.Add(CreateBranchPillItem("All branches", null, allBranchesCounts));

        foreach (var branch in branchNames)
        {
            var counts = _branchTabCounts.GetValueOrDefault(branch, new BranchWorkspaceHelper.BranchTabCounts(0, 0));
            items.Add(CreateBranchPillItem(branch, branch, counts));
        }

        return items;
    }

    private static BranchWorkspacePillItem CreateBranchPillItem(
        string label,
        string? branchKey,
        BranchWorkspaceHelper.BranchTabCounts counts) =>
        new()
        {
            BranchLabel = BranchWorkspaceHelper.FormatBranchPillLabel(label),
            BranchKey = branchKey,
            OpenCount = counts.OpenCount,
            UrgentCount = counts.ImmediateCount,
            BadgeText = BranchWorkspaceHelper.FormatBranchPillBadge(counts),
            TooltipText = BranchWorkspaceHelper.FormatBranchPillTooltip(label, counts)
        };

    private void ApplyStatusKpis(OperationsStatusSnapshot status)
    {
        OpenThreadCountText.Text = status.OpenThreadCount.ToString();
        HangingLeadCountText.Text = status.HangingLeadCount.ToString();
        RevenueAtRiskText.Text = UnifiedMessengerDashboardPresentationHelper.FormatRevenue(status.TotalRevenueAtRisk);
        AvgReplyTimeValue.Text = status.AverageReplyTime;
        SlaBreachesValue.Text = status.SlaBreaches;
        ResponseRateValue.Text = status.ResponseRate;
        ImmediateActionCountText.Text = status.ImmediateActionTotal.ToString();
        if (status.ImmediateActionTotal > status.ImmediateActionQueueCount &&
            status.ImmediateActionQueueCount > 0)
        {
            ToolTipService.SetToolTip(
                ImmediateActionKpiCard,
                $"{status.ImmediateActionTotal} urgent threads in scope. The action lane shows the top {status.ImmediateActionQueueCount}.");
        }
        else
        {
            ToolTipService.SetToolTip(
                ImmediateActionKpiCard,
                "All urgent threads in scope. The action lane shows the top 24 by urgency.");
        }
        PeakHourValue.Text = status.PeakHour;
        DailyTrendValue.Text = status.DailyTrend;
        ApplySubtext(AvgReplyTimeSubtext, status.AverageReplyTimeSubtext);
        ApplySubtext(SlaThresholdSubtext, status.SlaThresholdSubtext);
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

        _workspaceBranchKey = metric.BranchName;
        _showWorkspaceLoading = true;
        SelectWorkspacePill(metric.BranchName);
        _ = RefreshAsync(_professionalInstances, _registry);
    }

    private void SelectWorkspacePill(string branchName)
    {
        _suppressPillSelection = true;
        BranchWorkspacePillBar.SelectBranchKey(branchName);
        _suppressPillSelection = false;
    }

    private void RefreshBranchMetricSelection()
    {
        var selected = _workspaceBranchKey;
        var isScoped = IsWorkspaceBranchScoped();
        for (var index = 0; index < _branchMetrics.Count; index++)
        {
            var metric = _branchMetrics[index];
            var isSelected = !string.IsNullOrWhiteSpace(selected) &&
                             metric.BranchName.Equals(selected, StringComparison.OrdinalIgnoreCase);
            if (metric.IsSelected != isSelected || metric.IsWorkspaceScoped != isScoped)
            {
                _branchMetrics[index] = new BranchMetricViewModel(metric.Source, isSelected, isScoped);
            }
        }
    }

    private void ThreadCardList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationsThreadCardViewModel card &&
            !string.IsNullOrWhiteSpace(card.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(
                card.InstanceId,
                card.ConversationKey,
                card.CustomerName);
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
            ShellNavigationService.Instance.RequestInstance(
                item.InstanceId,
                item.ConversationKey,
                item.CustomerName);
        }
    }

    private void OperationalHighlightsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationalHighlightViewModel highlight &&
            !string.IsNullOrWhiteSpace(highlight.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(
                highlight.InstanceId,
                highlight.ConversationKey,
                highlight.Title);
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
            await ShowSimpleDialogAsync(
                "Select a review",
                "Choose a Google review alert from the list before inserting a draft reply.")
                .ConfigureAwait(true);
            return;
        }

        var replyText = ReviewReplyBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(replyText))
        {
            await ShowSimpleDialogAsync(
                "Draft reply is empty",
                "Type a reply in the text box, then choose Insert draft.")
                .ConfigureAwait(true);
            return;
        }

        ShellNavigationService.Instance.RequestInstance(_selectedReviewAlert.InstanceId);

        var reviewId = JsonSerializer.Serialize(_selectedReviewAlert.ReviewId);
        var reply = JsonSerializer.Serialize(replyText);
        var script = $"window.__umSubmitReviewReply({reviewId}, {reply});";

        try
        {
            await InstanceSessionManager.Instance.ExecuteScriptOnInstanceAsync(
                _selectedReviewAlert.InstanceId,
                script);
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync("Could not insert draft", ex.Message).ConfigureAwait(true);
        }
    }

    private async void OpenSelectedReviewInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        var instanceId = _selectedReviewAlert?.InstanceId ??
            _snapshot.PlatformIntelligence.GoogleInstanceIds.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            await ShowSimpleDialogAsync(
                "No Google instance",
                "Add a Google Business professional account to respond to reviews.")
                .ConfigureAwait(true);
            return;
        }

        ShellNavigationService.Instance.RequestInstance(instanceId);
    }

    private void AddProfessionalInstanceButton_Click(object sender, RoutedEventArgs e) =>
        ShellNavigationService.Instance.RequestAddInstance();

    private async void RefreshCommandButton_Click(object sender, RoutedEventArgs e) =>
        await RunRefreshCommandAsync(RefreshCommandButton).ConfigureAwait(true);

    private async void RefreshPlatformDataButton_Click(object sender, RoutedEventArgs e) =>
        await RunRefreshCommandAsync(RefreshPlatformDataButton).ConfigureAwait(true);

    private async Task RunRefreshCommandAsync(Button button)
    {
        button.IsEnabled = false;
        var originalContent = button.Content;
        button.Content = "Refreshing…";
        try
        {
            await RequestPlatformDataRefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            button.IsEnabled = true;
            button.Content = originalContent;
        }
    }

    private async void ExportAnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            await ShowSimpleDialogAsync(
                "Export unavailable",
                "Instance registry is not loaded yet. Try again after the dashboard finishes loading.")
                .ConfigureAwait(true);
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
                await MessageAnalyticsService.Instance.ExportCsvAsync(_snapshot.FilteredInstances, file.Path);
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

        public string? ConversationKey { get; } = string.IsNullOrWhiteSpace(item.Thread?.ConversationKey)
            ? null
            : item.Thread!.ConversationKey;

        public Visibility IntentLabelVisibility { get; } = string.IsNullOrWhiteSpace(item.IntentLabel)
            ? Visibility.Collapsed
            : Visibility.Visible;

        public Visibility UrgencyLabelVisibility { get; } = string.IsNullOrWhiteSpace(item.UrgencyLabel)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private sealed class BranchMetricViewModel
    {
        private const double DimmedCardOpacity = 0.55;

        public BranchMetricViewModel(UnifiedMessengerBranchMetrics metric, bool isSelected, bool isWorkspaceScoped)
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
            IsWorkspaceScoped = isWorkspaceScoped;
            LatencyBrush = CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));
            LatencyBorderBrush = CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));
            CardBackgroundBrush = isSelected
                ? ResolveThemeBrush("LayerFillColorAltBrush", Color.FromArgb(255, 239, 246, 255))
                : ResolveThemeBrush("CardBackgroundFillColorDefaultBrush", Colors.Transparent);
            CardOpacity = isWorkspaceScoped && !isSelected ? DimmedCardOpacity : 1.0;
            CardBorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            ToolTipText = isSelected ? "Currently scoped to this branch" : "Select branch tab";
            ScopeHintText = isSelected ? "Scoped workspace" : "Select branch tab";
            ScopeHintVisibility = Visibility.Visible;
        }

        public UnifiedMessengerBranchMetrics Source { get; }

        public string BranchName { get; }

        public string LatencyDisplay { get; }

        public string UnresolvedDisplay { get; }

        public string InboxDisplay { get; }

        public string PlatformBreakdown { get; }

        public string DetailDisplay { get; }

        public bool IsSelected { get; }

        public bool IsWorkspaceScoped { get; }

        public SolidColorBrush LatencyBrush { get; }

        public SolidColorBrush LatencyBorderBrush { get; }

        public SolidColorBrush CardBackgroundBrush { get; }

        public double CardOpacity { get; }

        public Thickness CardBorderThickness { get; }

        public string ToolTipText { get; }

        public string ScopeHintText { get; }

        public Visibility ScopeHintVisibility { get; }

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

        public string? ConversationKey { get; } = string.IsNullOrWhiteSpace(item.ConversationKey)
            ? null
            : item.ConversationKey;
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

    private static SolidColorBrush ResolveThemeBrush(string resourceKey, Color fallback) =>
        Application.Current.Resources.TryGetValue(resourceKey, out var resource) && resource is SolidColorBrush brush
            ? brush
            : new SolidColorBrush(fallback);

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

    private async Task ShowSimpleDialogAsync(string title, string message)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }
}
