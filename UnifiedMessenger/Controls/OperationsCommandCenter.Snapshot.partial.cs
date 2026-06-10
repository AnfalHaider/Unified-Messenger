using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void ApplySnapshot(OperationsCommandCenterSnapshot snapshot)
    {
        _snapshot = snapshot;
        var threadOps = snapshot.ThreadOperations;
        var status = snapshot.Status;
        var hasProfessional = _professionalInstances.Any();

        _viewModel.ApplyShellPresentation(OccSnapshotPresenter.BuildShellPresentation(
            hasProfessional,
            snapshot.ScopeLabel,
            DateTime.Now));
        ApplyShellUi();

        ApplyStatusKpis(OccSnapshotPresenter.BuildStatusKpis(status));
        ApplyKanban(threadOps);
        ApplyImmediateQueue(threadOps);

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

    private void ApplyShellUi()
    {
        EmptyStatePanel.Visibility = _viewModel.ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        MainContentScrollViewer.Visibility = _viewModel.ShowMainContent ? Visibility.Visible : Visibility.Collapsed;
        ScopeText.Text = _viewModel.ScopeLabel;
        LastRefreshedText.Text = _viewModel.LastRefreshedText;
        ApplyBranchFilterChip();
    }

    private void ApplyBranchFilterChip()
    {
        var hasFilter = !string.IsNullOrWhiteSpace(_workspaceBranchKey);
        BranchFilterChipPanel.Visibility = hasFilter ? Visibility.Visible : Visibility.Collapsed;
        if (hasFilter)
        {
            BranchFilterChipText.Text = $"Branch: {_workspaceBranchKey}";
        }
    }

    private void ClearBranchFilterButton_Click(object sender, RoutedEventArgs e) =>
        SelectWorkspaceBranch(null);

    private bool IsWorkspaceBranchSelected(string branchName) =>
        !string.IsNullOrWhiteSpace(_workspaceBranchKey) &&
        branchName.Equals(_workspaceBranchKey, StringComparison.OrdinalIgnoreCase);

    private bool IsWorkspaceBranchScoped() =>
        !string.IsNullOrWhiteSpace(_workspaceBranchKey);

    private bool ShouldHideBranchOnCards() =>
        !string.IsNullOrWhiteSpace(_workspaceBranchKey);

    private void ApplyKanban(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();

        SyncThreadCards(
            _viewModel.NewInquiries,
            OccThreadCardPresenter
                .BuildKanbanColumn(threadOps.AllThreads, UnifiedMessengerKanbanColumn.NewInquiries, hideBranch)
                .ToList());
        SyncThreadCards(
            _viewModel.HangingLeads,
            OccThreadCardPresenter
                .BuildKanbanColumn(threadOps.AllThreads, UnifiedMessengerKanbanColumn.HangingLeads, hideBranch)
                .ToList());
        SyncThreadCards(
            _viewModel.Resolved,
            OccThreadCardPresenter
                .BuildKanbanColumn(threadOps.AllThreads, UnifiedMessengerKanbanColumn.Resolved, hideBranch)
                .ToList());

        _viewModel.ApplyKanbanEmptyStates(
            _viewModel.NewInquiries.Count,
            _viewModel.HangingLeads.Count,
            _viewModel.Resolved.Count);
        KanbanBoard.UpdateEmptyStates(
            _viewModel.NewInquiries.Count,
            _viewModel.HangingLeads.Count,
            _viewModel.Resolved.Count);
    }

    private void ApplyImmediateQueue(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();
        SyncThreadCards(
            _viewModel.ImmediateQueue,
            OccThreadCardPresenter.BuildThreadCards(threadOps.ImmediateActionQueue, hideBranch));

        _viewModel.ApplyImmediateQueuePresentation(
            OccSnapshotPresenter.BuildImmediateQueuePresentation(threadOps));
        ImmediateQueueEmptyText.Visibility = _viewModel.ShowImmediateQueueEmpty
            ? Visibility.Visible
            : Visibility.Collapsed;
        ImmediateQueueFooterText.Visibility = _viewModel.ShowImmediateQueueFooter
            ? Visibility.Visible
            : Visibility.Collapsed;
        ImmediateQueueFooterText.Text = _viewModel.ImmediateQueueFooterText ?? string.Empty;
    }

    private static void SyncThreadCards(
        ObservableCollection<OperationsThreadCardViewModel> target,
        IReadOnlyList<OperationsThreadCardViewModel> source) =>
        ObservableCollectionSyncHelper.Sync(
            target,
            source,
            card => card.ThreadId,
            OperationsThreadCardSync.ContentEquals);

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

        var dismissed = _services.AppSettings.Settings.OccPlatformIntelligenceDismissedSignal;

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

        var dismissed = _services.AppSettings.Settings.OccAnalyticsTrendsDismissedSignal;

        if (DashboardCardEmptyStateHelper.ShouldAutoExpandAnalyticsTrends(status, analytics) &&
            DashboardCardEmptyStateHelper.ShouldExpandOccSection(signal, dismissed))
        {
            AnalyticsTrendsExpander.IsExpanded = true;
        }
    }

    private void PersistOccDismissedSignal(int signal, Action<AppSettings, int> assignDismissedSignal) =>
        _ = _services.AppSettings.UpdateAsync(settings => assignDismissedSignal(settings, signal));

    private void ResetOccDismissedSignalIfNeeded(
        Func<AppSettings, int> readDismissedSignal,
        Action<AppSettings, int> assignDismissedSignal)
    {
        if (readDismissedSignal(_services.AppSettings.Settings) <= 0)
        {
            return;
        }

        _ = _services.AppSettings.UpdateAsync(settings => assignDismissedSignal(settings, 0));
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
        KanbanBoard.WireScrollBubbling(MainContentScrollViewer);
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
            _services.AppSettings.Settings.EnableLocalAi,
            triage.TotalTriageCount,
            _snapshot.AiInsightFeed.Count);
        AiInsightFeedEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatExecutiveInsightsEmptyMessage(emptyReason);
        AiInsightFeedEmptyText.Visibility = Visibility.Visible;
    }

    private void RebuildBranchPills(IReadOnlyList<string> branchNames)
    {
        var pillBar = OccSnapshotPresenter.BuildPillBar(_branchTabCounts, branchNames);

        if (!string.Equals(pillBar.Signature, _lastPillBarSignature, StringComparison.Ordinal))
        {
            _lastPillBarSignature = pillBar.Signature;
            _suppressPillSelection = true;
            BranchWorkspacePillBar.SetItems(pillBar.Items, _workspaceBranchKey);
            _suppressPillSelection = false;
            return;
        }

        _suppressPillSelection = true;
        BranchWorkspacePillBar.SelectBranchKey(_workspaceBranchKey);
        _suppressPillSelection = false;
    }

    private void ApplyStatusKpis(OccStatusKpiPresentation status)
    {
        OpenKpiCard.Value = status.OpenThreadCount;
        HangingKpiCard.Value = status.HangingLeadCount;
        RevenueKpiCard.Value = status.RevenueAtRisk;
        AvgReplyTimeValue.Text = status.AverageReplyTime;
        SlaBreachesKpiCard.Value = status.SlaBreaches;
        ResponseRateValue.Text = status.ResponseRate;
        ImmediateActionKpiCard.Value = status.ImmediateActionCount;
        PeakHourValue.Text = status.PeakHour;
        DailyTrendValue.Text = status.DailyTrend;
        ApplySubtext(AvgReplyTimeSubtext, status.AverageReplyTimeSubtext);
        SlaBreachesKpiCard.Subtext = status.SlaThresholdSubtext ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(status.ImmediateActionTooltip))
        {
            ToolTipService.SetToolTip(ImmediateActionKpiCard, status.ImmediateActionTooltip);
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
