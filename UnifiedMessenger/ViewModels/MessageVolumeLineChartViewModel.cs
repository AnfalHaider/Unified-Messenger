using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class MessageVolumeLineChartViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _summaryText = "No message volume in the selected range.";

    [ObservableProperty]
    private string _linePathData = string.Empty;

    [ObservableProperty]
    private string _areaPathData = string.Empty;

    [ObservableProperty]
    private bool _showEmptyHint = true;

    public void ApplySeries(
        IReadOnlyList<DailyActivityPoint>? series,
        double width = 320,
        double height = 96)
    {
        var chart = MessageVolumeLineChartHelper.Build(series, width, height);
        SummaryText = chart.SummaryText;
        LinePathData = chart.LinePathData;
        AreaPathData = chart.AreaPathData;
        ShowEmptyHint = string.IsNullOrWhiteSpace(chart.LinePathData);
    }
}
