using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void InitializeDateRangePickers()
    {
        var today = DateTimeOffset.Now;
        _suppressDateRangeEvents = true;
        try
        {
            if (_services.OccDateRangeFilter.FromUtc is { } from)
            {
                FromDatePicker.Date = from;
            }
            else
            {
                FromDatePicker.Date = today.AddDays(-6);
            }

            if (_services.OccDateRangeFilter.ToUtc is { } to)
            {
                ToDatePicker.Date = to;
            }
            else
            {
                ToDatePicker.Date = today;
            }
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

        _dispatcherQueue.TryEnqueue(() => _ = RefreshAsync(_professionalInstances, _registry));
    }

    private void DateRangePicker_DateChanged(object sender, DatePickerValueChangedEventArgs args)
    {
        if (_suppressDateRangeEvents)
        {
            return;
        }

        if (ReferenceEquals(sender, FromDatePicker))
        {
            _services.OccDateRangeFilter.FromUtc = args.NewDate;
        }
        else if (ReferenceEquals(sender, ToDatePicker))
        {
            _services.OccDateRangeFilter.ToUtc = args.NewDate;
        }
    }

    private void ClearDateRangeButton_Click(object sender, RoutedEventArgs e)
    {
        _services.OccDateRangeFilter.Clear();
        InitializeDateRangePickers();
        _ = RefreshAsync(_professionalInstances, _registry);
    }

    private void ApplyAnalyticsTrends(OperationsAnalyticsTrendSnapshot trends)
    {
        MessageVolumeChart.ApplySeries(trends.WeeklyActivity);
    }
}
