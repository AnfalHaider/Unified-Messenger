using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool _suppressQueueFilterEvents;
    private bool _suppressBoardViewEvents;
    private bool _suppressViewModeEvents;

    private void EnsureOccLayoutSettings()
    {
        var settings = _services.AppSettings.Settings;
        if (settings.OccDefaultBoardViewExpanded is null && settings.Version < 17)
        {
            settings.OccDefaultBoardViewExpanded = true;
        }
        else if (settings.OccDefaultBoardViewExpanded is null)
        {
            settings.OccDefaultBoardViewExpanded = false;
        }

        if (!_suppressBoardViewEvents)
        {
            _suppressBoardViewEvents = true;
            BoardViewToggle.IsChecked = settings.OccBoardViewExpanded;
            KanbanBoard.Visibility = settings.OccBoardViewExpanded ? Visibility.Visible : Visibility.Collapsed;
            _suppressBoardViewEvents = false;
        }
    }

    private void EnsureQueueTeachingTip()
    {
        if (_services.AppSettings.Settings.OccQueueTeachingTipSeen)
        {
            return;
        }

        QueueUxTeachingTip.Target = WorkQueueSection;
        QueueUxTeachingTip.IsOpen = true;
        _services.AppSettings.Settings.OccQueueTeachingTipSeen = true;
        _ = _services.AppSettings.SaveAsync();
    }

    private void ApplyWorkQueue(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();
        var filter = _services.OccQueueFilter.Filter;
        var compact = _services.AppSettings.Settings.OccCompactCardDensity;
        var queueThreads = _services.Dashboard.BuildWorkQueue(
            threadOps.AllThreads.Where(thread => !thread.IsSpamOrPromo),
            filter);

        SyncThreadCards(
            _viewModel.WorkQueue,
            OccThreadCardPresenter
                .BuildThreadCards(queueThreads, hideBranch, _services.AiInferenceQueue, compact)
                .ToList());

        var presentation = OccSnapshotPresenter.BuildWorkQueuePresentation(queueThreads, filter);
        WorkQueueEmptyText.Text = presentation.EmptyHint;
        WorkQueueEmptyText.Visibility = presentation.ShowEmptyState
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyDataScopeManifest(threadOps, queueThreads.Count);
    }

    private void ApplyDataScopeManifest(UnifiedMessengerDashboardSnapshot threadOps, int queueCount)
    {
        var manifest = OccDataScopeManifestHelper.Build(
            _services.OccViewMode.Mode,
            _services.OccDateRangeFilter.FromUtc,
            _services.OccDateRangeFilter.ToUtc,
            queueCount);

        DateRangeScopeCaption.Text = manifest.BannerBody;

        if (manifest.ShowHistoricalBanner)
        {
            HistoricalDataBanner.Title = manifest.BannerTitle;
            HistoricalDataBanner.Message = manifest.BannerBody;
            HistoricalDataBanner.IsOpen = true;
            HistoricalActionButtons.Visibility = Visibility.Visible;
            RefreshCommandButton.Content = "Reload snapshot";
        }
        else
        {
            HistoricalDataBanner.IsOpen = false;
            HistoricalActionButtons.Visibility = Visibility.Collapsed;
            RefreshCommandButton.Content = "Refresh";
        }
    }

    private void SyncQueueFilterChips()
    {
        _suppressQueueFilterEvents = true;
        try
        {
            FilterAllOpenChip.IsChecked = _services.OccQueueFilter.Filter == OccQueueFilter.AllOpen;
            FilterUrgentChip.IsChecked = _services.OccQueueFilter.Filter == OccQueueFilter.Urgent;
            FilterSlaChip.IsChecked = _services.OccQueueFilter.Filter == OccQueueFilter.SlaBreach;
            FilterHangingChip.IsChecked = _services.OccQueueFilter.Filter == OccQueueFilter.Hanging;
        }
        finally
        {
            _suppressQueueFilterEvents = false;
        }
    }

    private void QueueFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressQueueFilterEvents)
        {
            return;
        }

        var filter = OccQueueFilter.AllOpen;
        if (ReferenceEquals(sender, FilterUrgentChip))
        {
            filter = OccQueueFilter.Urgent;
        }
        else if (ReferenceEquals(sender, FilterSlaChip))
        {
            filter = OccQueueFilter.SlaBreach;
        }
        else if (ReferenceEquals(sender, FilterHangingChip))
        {
            filter = OccQueueFilter.Hanging;
        }

        _services.OccQueueFilter.Filter = filter;
        SyncQueueFilterChips();
        if (_snapshot.ThreadOperations.AllThreads.Count > 0)
        {
            ApplyWorkQueue(_snapshot.ThreadOperations);
        }
    }

    private async void BoardViewToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressBoardViewEvents)
        {
            return;
        }

        var expanded = BoardViewToggle.IsChecked == true;
        KanbanBoard.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

        try
        {
            await _services.AppSettings.UpdateAsync(settings =>
                settings.OccBoardViewExpanded = expanded).ConfigureAwait(true);
        }
        catch
        {
            // Non-fatal.
        }
    }

    private void SyncRecentHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        SyncRecentHistoryButton.IsEnabled = false;
        try
        {
            var instances = OccChartBackfillHelper.ResolveConnectedWhatsAppInstances(
                _professionalInstances,
                WorkspaceBranchKey);
            var manager = BackfillSyncManager.Instance;
            foreach (var instance in instances)
            {
                manager.Schedule(instance, force: true);
            }

            ApplyBackfillStatusUi();
        }
        finally
        {
            SyncRecentHistoryButton.IsEnabled = true;
        }
    }

    private async void ReloadSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync(_professionalInstances, _registry).ConfigureAwait(true);
    }

    private void OnOccQueueFilterChanged(object? sender, EventArgs e) =>
        _dispatcherQueue.TryEnqueue(SyncQueueFilterChips);

    public void SelectUrgentQueueFilter()
    {
        _services.OccQueueFilter.Filter = OccQueueFilter.Urgent;
        SyncQueueFilterChips();
        if (_snapshot.ThreadOperations.AllThreads.Count > 0)
        {
            ApplyWorkQueue(_snapshot.ThreadOperations);
        }
        else if (_registry is not null)
        {
            _ = RefreshAsync(_professionalInstances, _registry);
        }

        WorkQueueSection?.StartBringIntoView();
    }

    private bool ShouldHideBranchOnCards() =>
        !string.IsNullOrWhiteSpace(WorkspaceBranchKey);

    private void ApplyKanban(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();

        SyncThreadCards(
            _viewModel.NewInquiries,
            OccThreadCardPresenter
                .BuildKanbanColumn(
                    threadOps.AllThreads,
                    UnifiedMessengerKanbanColumn.NewInquiries,
                    hideBranch,
                    _services.AiInferenceQueue)
                .ToList());
        SyncThreadCards(
            _viewModel.HangingLeads,
            OccThreadCardPresenter
                .BuildKanbanColumn(
                    threadOps.AllThreads,
                    UnifiedMessengerKanbanColumn.HangingLeads,
                    hideBranch,
                    _services.AiInferenceQueue)
                .ToList());
        SyncThreadCards(
            _viewModel.Resolved,
            OccThreadCardPresenter
                .BuildKanbanColumn(
                    threadOps.AllThreads,
                    UnifiedMessengerKanbanColumn.Resolved,
                    hideBranch,
                    _services.AiInferenceQueue)
                .ToList());

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
            OccThreadCardPresenter.BuildThreadCards(
                threadOps.ImmediateActionQueue,
                hideBranch,
                _services.AiInferenceQueue));

        _viewModel.ApplyImmediateQueuePresentation(
            OccSnapshotPresenter.BuildImmediateQueuePresentation(threadOps));
    }

    private static void SyncThreadCards(
        ObservableCollection<OperationsThreadCardViewModel> target,
        IReadOnlyList<OperationsThreadCardViewModel> source) =>
        ObservableCollectionSyncHelper.Sync(
            target,
            source,
            card => card.ThreadId,
            OperationsThreadCardSync.ContentEquals);

    private void KanbanBoard_ColumnOrderChanged(
        object sender,
        (string ColumnKey, IReadOnlyList<string> OrderedThreadIds) e) =>
        _services.ThreadDisplayOrder.UpdateColumnOrder(e.ColumnKey, e.OrderedThreadIds);

    private async void KanbanBoard_ColumnTransferRequested(
        object sender,
        (OperationsThreadCardViewModel Card, string SourceColumnKey, string TargetColumnKey) e)
    {
        if (!Enum.TryParse<UnifiedMessengerKanbanColumn>(e.TargetColumnKey, out var targetColumn))
        {
            return;
        }

        _services.ThreadRegistry.SetThreadKanbanColumn(e.Card.ThreadId, targetColumn);
        _services.ThreadDisplayOrder.RemoveFromColumn(e.SourceColumnKey, e.Card.ThreadId);
        _services.ThreadDisplayOrder.MoveToTop(e.TargetColumnKey, e.Card.ThreadId);

        await RefreshAsync(_professionalInstances, _registry).ConfigureAwait(true);
    }

    private void KanbanBoard_ItemClickRequested(object sender, OperationsThreadCardViewModel card)
    {
        _keyboardMoveThreadId = card.ThreadId;
        _keyboardMoveColumnKey = ResolveColumnKeyForThread(card.ThreadId);

        if (!string.IsNullOrWhiteSpace(card.InstanceId))
        {
            NavigateToThreadCard(card);
        }
    }

    private void KanbanBoard_CrossColumnDragAttempted(object sender, EventArgs e)
    {
        // Cross-column transfer is handled by ColumnTransferRequested.
    }

    private void InitializeViewModeToggle()
    {
        _suppressViewModeEvents = true;
        try
        {
            OccViewModeToggle.IsOn = _services.OccViewMode.IsHistorical;
        }
        finally
        {
            _suppressViewModeEvents = false;
        }

        MessageVolumeChart.IsHistoricalMode = _services.OccViewMode.IsHistorical;
    }

    private void OnOccViewModeChanged(object? sender, EventArgs e) =>
        _dispatcherQueue.TryEnqueue(() =>
        {
            InitializeViewModeToggle();
            ApplyScopeLabel(null, null);
            _ = RefreshAsync(_professionalInstances, _registry);
        });

    private async void OccViewModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressViewModeEvents)
        {
            return;
        }

        _services.OccViewMode.Mode = OccViewModeToggle.IsOn
            ? OccViewMode.Historical
            : OccViewMode.Live;

        MessageVolumeChart.IsHistoricalMode = _services.OccViewMode.IsHistorical;

        try
        {
            await _services.AppSettings.UpdateAsync(settings =>
                    OccViewModeSettingsHelper.WriteToSettings(settings, _services.OccViewMode))
                .ConfigureAwait(true);
        }
        catch
        {
            // Non-fatal — mode still applies for this session.
        }
    }
}
