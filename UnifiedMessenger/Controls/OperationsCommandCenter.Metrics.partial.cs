using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private const int DateRangeDebounceMilliseconds = 300;

    private void OpenThreadsCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.OpenThreads);

    private void HangingLeadsCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.HangingLeads);

    private void UrgentCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.Urgent);

    private void SlaBreachesCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.SlaBreaches);

    private async void NavigateKpi(OccKpiKind kind)
    {
        var threads = _snapshot.ThreadOperations.AllThreads;
        var target = OccKpiNavigationHelper.ResolveTarget(kind, threads);
        if (target is null)
        {
            return;
        }

        SetWorkspaceLoadingVisible(true);
        try
        {
            await ConversationNavigationCoordinator.NavigateToThreadAsync(
                _services.SessionManager,
                _services.Registry,
                _services.ThreadRegistry,
                _services.Navigation,
                target.InstanceId,
                target.ConversationKey,
                target.CustomerName,
                target.ThreadId).ConfigureAwait(true);
        }
        finally
        {
            SetWorkspaceLoadingVisible(false);
        }
    }

    private void InitializeDateRangePickers()
    {
        _suppressDateRangeEvents = true;
        try
        {
            var from = _services.OccDateRangeFilter.FromUtc ?? DateTimeOffset.Now.AddDays(-(OccDateRangeFilterState.DefaultWindowDays - 1));
            var to = _services.OccDateRangeFilter.ToUtc ?? DateTimeOffset.Now;
            FromDatePicker.Date = from;
            ToDatePicker.Date = to;
        }
        finally
        {
            _suppressDateRangeEvents = false;
        }
    }

    private void OnOccDateRangeFilterChanged(object? sender, EventArgs e)
    {
        if (_suppressDateRangeEvents)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(ScheduleDateRangeRefresh);
    }

    private void DateRangePicker_DateChanged(object sender, DatePickerValueChangedEventArgs args)
    {
        if (_suppressDateRangeEvents)
        {
            return;
        }

        ApplyPickerValuesToFilterState();
        ScheduleDateRangeRefresh();
    }

    private void ClearDateRangeButton_Click(object sender, RoutedEventArgs e)
    {
        _suppressDateRangeEvents = true;
        try
        {
            _services.OccDateRangeFilter.ResetToDefaultWindow();
            InitializeDateRangePickers();
        }
        finally
        {
            _suppressDateRangeEvents = false;
        }

        _ = PersistDateRangeAsync();
    }

    private void ApplyPickerValuesToFilterState()
    {
        var from = FromDatePicker.Date;
        var to = ToDatePicker.Date;
        if (from > to)
        {
            (from, to) = (to, from);
            _suppressDateRangeEvents = true;
            try
            {
                FromDatePicker.Date = from;
                ToDatePicker.Date = to;
            }
            finally
            {
                _suppressDateRangeEvents = false;
            }
        }

        _suppressDateRangeEvents = true;
        try
        {
            _services.OccDateRangeFilter.FromUtc = from;
            _services.OccDateRangeFilter.ToUtc = to;
        }
        finally
        {
            _suppressDateRangeEvents = false;
        }

        _ = PersistDateRangeAsync();
    }

    private void ScheduleDateRangeRefresh()
    {
        _dateRangeDebounceTimer ??= _dispatcherQueue.CreateTimer();
        _dateRangeDebounceTimer.Interval = TimeSpan.FromMilliseconds(DateRangeDebounceMilliseconds);
        _dateRangeDebounceTimer.Tick -= OnDateRangeDebounceTick;
        _dateRangeDebounceTimer.Tick += OnDateRangeDebounceTick;
        _dateRangeDebounceTimer.Stop();
        _dateRangeDebounceTimer.Start();
    }

    private void OnDateRangeDebounceTick(DispatcherQueueTimer sender, object args)
    {
        StopDateRangeDebounceTimer();
        _ = RefreshAsync(_professionalInstances, _registry);
    }

    private void StopDateRangeDebounceTimer()
    {
        if (_dateRangeDebounceTimer is null)
        {
            return;
        }

        _dateRangeDebounceTimer.Tick -= OnDateRangeDebounceTick;
        _dateRangeDebounceTimer.Stop();
    }

    private async Task PersistDateRangeAsync()
    {
        try
        {
            await _services.AppSettings.UpdateAsync(settings =>
                    OccDateRangeSettingsHelper.WriteToSettings(settings, _services.OccDateRangeFilter))
                .ConfigureAwait(true);
        }
        catch
        {
            // Non-fatal — chart range still applies for this session.
        }
    }

    private void ApplyAnalyticsTrends(OperationsAnalyticsTrendSnapshot trends)
    {
        MessageVolumeChart.IsHistoricalMode = _services.OccViewMode.IsHistorical;
        var exceedsCap = OccDateRangeFilterHelper.ExceedsChartDisplayCap(
            _services.OccDateRangeFilter.FromUtc,
            _services.OccDateRangeFilter.ToUtc);
        MessageVolumeChart.ApplySeries(trends.WeeklyActivity, exceedsCap);

        // P1-1: surface a clear "Sync message history" CTA only when the volume trend is empty,
        // so the most prominent panel offers a next step instead of a dead-end empty state.
        SyncHistoryCtaButton.Visibility = trends.HasMessageVolume
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
