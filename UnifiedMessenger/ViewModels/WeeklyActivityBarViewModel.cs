namespace UnifiedMessenger.ViewModels;

public sealed class WeeklyActivityBarViewModel
{
    public required string Label { get; init; }

    public double SentBarHeight { get; init; }

    public double ReceivedBarHeight { get; init; }

    public required string ToolTipText { get; init; }
}
