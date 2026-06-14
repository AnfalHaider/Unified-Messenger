using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool _suppressQueueFilterEvents;
    private bool _suppressBoardViewEvents;

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
}
