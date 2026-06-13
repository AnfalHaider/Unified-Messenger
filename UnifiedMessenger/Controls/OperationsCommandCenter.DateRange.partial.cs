using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private const int DateRangeDebounceMilliseconds = 300;

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
    }
}
