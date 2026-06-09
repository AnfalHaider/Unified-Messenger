namespace UnifiedMessenger.ViewModels;

public sealed class SentimentActivityBarViewModel
{
    public required string Label { get; init; }

    public double PositiveBarHeight { get; init; }

    public double NeutralBarHeight { get; init; }

    public double NegativeBarHeight { get; init; }

    public int DayTotal { get; init; }

    public string DayTotalText => DayTotal > 0 ? DayTotal.ToString() : string.Empty;

    public double DayTotalLabelOpacity => DayTotal > 0 ? 1.0 : 0.0;

    public required string ToolTipText { get; init; }
}
