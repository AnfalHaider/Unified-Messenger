namespace UnifiedMessenger.Models;

/// <summary>
/// Grid placement for a single Operations Command Center panel.
/// </summary>
public sealed class OccPanelPlacement
{
    public string PanelId { get; set; } = string.Empty;

    public int Column { get; set; }

    public int Row { get; set; }

    public int ColumnSpan { get; set; } = OccLayoutGridConstants.DefaultColumnSpan;

    public int RowSpan { get; set; } = 1;

    public bool IsVisible { get; set; } = true;

    public int MinHeightDp { get; set; }

    public OccPanelPlacement Clone() => new()
    {
        PanelId = PanelId,
        Column = Column,
        Row = Row,
        ColumnSpan = ColumnSpan,
        RowSpan = RowSpan,
        IsVisible = IsVisible,
        MinHeightDp = MinHeightDp
    };
}

public static class OccLayoutGridConstants
{
    public const int GridColumns = 12;

    public const int LeftColumn = 0;

    public const int LeftColumnSpan = 6;

    public const int RightColumn = 6;

    public const int RightColumnSpan = 6;

    public const int DefaultColumnSpan = 6;

    public const int FullWidthSpan = 12;

    public const double BreakpointWide = 960;

    public const double BreakpointMedium = 600;

    public const double BreakpointNarrow = 400;
}
