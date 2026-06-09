using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class WeeklyActivityChartViewModel : ViewModelBase
{
    public ObservableCollection<WeeklyActivityBarViewModel> Bars { get; } = [];

    [ObservableProperty]
    private string _summaryText = "No activity recorded yet.";

    [ObservableProperty]
    private bool _showEmptyHint;

    public void ApplySeries(IReadOnlyList<DailyActivityPoint>? series)
    {
        var summary = WeeklyActivityChartHelper.BuildSummary(series);
        SummaryText = summary.SummaryText;

        Bars.Clear();
        if (series is null || series.Count == 0)
        {
            ShowEmptyHint = true;
            return;
        }

        ShowEmptyHint = false;
        var max = WeeklyActivityChartHelper.ComputeScaleMaximum(series);
        const double maxHeight = 100;

        foreach (var point in series)
        {
            var sentHeight = WeeklyActivityChartHelper.ComputeBarHeight(point.Sent, max, maxHeight);
            var receivedHeight = WeeklyActivityChartHelper.ComputeBarHeight(point.Received, max, maxHeight);
            var dailyTotal = point.Sent + point.Received;

            Bars.Add(new WeeklyActivityBarViewModel
            {
                Label = point.Label,
                SentBarHeight = sentHeight,
                ReceivedBarHeight = receivedHeight,
                ToolTipText = $"{point.Label}: {point.Sent} sent, {point.Received} received ({dailyTotal} total)"
            });
        }
    }
}
