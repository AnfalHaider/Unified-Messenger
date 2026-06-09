using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class SentimentActivityChartViewModel : ViewModelBase
{
    public ObservableCollection<SentimentActivityBarViewModel> Bars { get; } = [];

    [ObservableProperty]
    private string _summaryText = "Awaiting classified messages";

    [ObservableProperty]
    private bool _showEmptyHint;

    public void ApplySeries(MessageTriageDashboardSnapshot? snapshot)
    {
        snapshot ??= MessageTriageDashboardSnapshot.Empty;
        SummaryText = DashboardTriageHelper.FormatSentimentSummary(snapshot);

        Bars.Clear();
        var series = snapshot.WeeklySentiment;
        if (series.Count == 0 ||
            series.All(point => point.Positive + point.Neutral + point.Negative == 0))
        {
            ShowEmptyHint = true;
            return;
        }

        ShowEmptyHint = false;
        var max = DashboardTriageHelper.ComputeSentimentScaleMaximum(series);
        const double maxHeight = 90;

        foreach (var point in series)
        {
            var dayTotal = point.Positive + point.Neutral + point.Negative;
            Bars.Add(new SentimentActivityBarViewModel
            {
                Label = point.Label,
                PositiveBarHeight = DashboardTriageHelper.ComputeBarHeight(point.Positive, max, maxHeight),
                NeutralBarHeight = DashboardTriageHelper.ComputeBarHeight(point.Neutral, max, maxHeight),
                NegativeBarHeight = DashboardTriageHelper.ComputeBarHeight(point.Negative, max, maxHeight),
                DayTotal = dayTotal,
                ToolTipText =
                    $"{point.Label}: {point.Positive} positive, {point.Neutral} neutral, {point.Negative} negative"
            });
        }
    }
}
