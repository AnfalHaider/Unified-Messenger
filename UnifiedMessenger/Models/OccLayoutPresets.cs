namespace UnifiedMessenger.Models;

public static class OccLayoutPresets
{
    public const string OperationsFocus = "operations-focus";

    public const string AnalyticsFocus = "analytics-focus";

    public const string Compact = "compact";

    public const string WhatsAppFocus = "whatsapp-focus";

    public const string FrontDesk = "front-desk";

    public const string Manager = "manager";

    public const string AfterHours = "after-hours";

    public static readonly IReadOnlyList<string> All =
    [
        OperationsFocus,
        AnalyticsFocus,
        Compact,
        WhatsAppFocus,
        FrontDesk,
        Manager,
        AfterHours
    ];

    public static IReadOnlyList<OccPanelPlacement> Create(string presetId) =>
        presetId switch
        {
            AnalyticsFocus => CreateAnalyticsFocus(),
            Compact => CreateCompact(),
            WhatsAppFocus => CreateWhatsAppFocus(),
            FrontDesk => CreateFrontDesk(),
            Manager => CreateManager(),
            AfterHours => CreateAfterHours(),
            _ => CreateOperationsFocus()
        };

    public static IReadOnlyList<OccPanelPlacement> CreateOperationsFocus() =>
    [
        Panel(OccLayoutDefaults.KpiStripPanelId, 0, 0, OccLayoutGridConstants.FullWidthSpan, 1, 72),
        Panel(OccLayoutDefaults.ImmediateLanePanelId, 0, 1, 5, 1, 240),
        Panel(OccLayoutDefaults.KanbanPanelId, 5, 1, 7, 1, 320),
        Panel(OccLayoutDefaults.BranchMetricsPanelId, 0, 2, 12, 1, 100),
        Panel(OccLayoutDefaults.BranchPulsePanelId, 0, 3, 6, 1, 160),
        Panel(OccLayoutDefaults.AiFeedPanelId, 6, 3, 6, 1, 160),
        Panel(OccLayoutDefaults.HighlightsPanelId, 0, 4, 6, 1, 120),
        Panel(OccLayoutDefaults.PlatformIntelligencePanelId, 6, 4, 6, 1, 120),
        Panel(OccLayoutDefaults.AnalyticsPanelId, 0, 5, 6, 1, 160),
        Panel(OccLayoutDefaults.DataHealthPanelId, 6, 5, 6, 1, 80)
    ];

    public static IReadOnlyList<OccPanelPlacement> CreateAnalyticsFocus() =>
    [
        Panel(OccLayoutDefaults.KpiStripPanelId, 0, 0, OccLayoutGridConstants.FullWidthSpan, 1, 72),
        Panel(OccLayoutDefaults.ImmediateLanePanelId, 0, 1, 4, 1, 200),
        Panel(OccLayoutDefaults.KanbanPanelId, 4, 1, 8, 1, 280),
        Panel(OccLayoutDefaults.AnalyticsPanelId, 0, 2, 8, 1, 200),
        Panel(OccLayoutDefaults.PlatformIntelligencePanelId, 8, 2, 4, 1, 160),
        Panel(OccLayoutDefaults.HighlightsPanelId, 0, 3, 4, 1, 120),
        Panel(OccLayoutDefaults.AiFeedPanelId, 4, 3, 4, 1, 160),
        Panel(OccLayoutDefaults.DataHealthPanelId, 8, 3, 4, 1, 80),
        Panel(OccLayoutDefaults.BranchMetricsPanelId, 0, 4, 12, 1, 100)
    ];

    public static IReadOnlyList<OccPanelPlacement> CreateWhatsAppFocus() =>
    [
        Panel(OccLayoutDefaults.KpiStripPanelId, 0, 0, OccLayoutGridConstants.FullWidthSpan, 1, 72),
        Panel(OccLayoutDefaults.ImmediateLanePanelId, 0, 1, 5, 1, 280),
        Panel(OccLayoutDefaults.KanbanPanelId, 5, 1, 7, 1, 360),
        Panel(OccLayoutDefaults.BranchMetricsPanelId, 0, 2, OccLayoutGridConstants.FullWidthSpan, 1, 100),
        Panel(OccLayoutDefaults.BranchPulsePanelId, 0, 3, OccLayoutGridConstants.FullWidthSpan, 1, 180),
        Panel(OccLayoutDefaults.AiFeedPanelId, 0, 4, 6, 1, 200),
        Panel(OccLayoutDefaults.HighlightsPanelId, 6, 4, 6, 1, 140),
        Panel(OccLayoutDefaults.DataHealthPanelId, 0, 5, OccLayoutGridConstants.FullWidthSpan, 1, 80)
    ];

    public static IReadOnlyList<OccPanelPlacement> CreateCompact()
    {
        var panels = OccLayoutDefaults.ContextPanelOrder
            .Prepend(OccLayoutDefaults.KanbanPanelId)
            .Prepend(OccLayoutDefaults.ImmediateLanePanelId)
            .Prepend(OccLayoutDefaults.KpiStripPanelId)
            .ToList();

        var placements = new List<OccPanelPlacement>(panels.Count);
        for (var row = 0; row < panels.Count; row++)
        {
            placements.Add(Panel(panels[row], 0, row, OccLayoutGridConstants.FullWidthSpan, 1));
        }

        return placements;
    }

    public static IReadOnlyList<OccPanelPlacement> CreateFrontDesk() =>
    [
        Panel(OccLayoutDefaults.KpiStripPanelId, 0, 0, OccLayoutGridConstants.FullWidthSpan, 1, 72),
        Panel(OccLayoutDefaults.ImmediateLanePanelId, 0, 1, 6, 2, 360),
        Panel(OccLayoutDefaults.KanbanPanelId, 6, 1, 6, 2, 400),
        Panel(OccLayoutDefaults.BranchMetricsPanelId, 0, 3, OccLayoutGridConstants.FullWidthSpan, 1, 80)
    ];

    public static IReadOnlyList<OccPanelPlacement> CreateManager() =>
    [
        Panel(OccLayoutDefaults.KpiStripPanelId, 0, 0, OccLayoutGridConstants.FullWidthSpan, 1, 72),
        Panel(OccLayoutDefaults.BranchMetricsPanelId, 0, 1, OccLayoutGridConstants.FullWidthSpan, 1, 120),
        Panel(OccLayoutDefaults.BranchPulsePanelId, 0, 2, 6, 1, 200),
        Panel(OccLayoutDefaults.ImmediateLanePanelId, 0, 3, 4, 1, 220),
        Panel(OccLayoutDefaults.KanbanPanelId, 4, 3, 8, 1, 300),
        Panel(OccLayoutDefaults.AiFeedPanelId, 6, 2, 6, 1, 200)
    ];

    public static IReadOnlyList<OccPanelPlacement> CreateAfterHours() =>
    [
        Panel(OccLayoutDefaults.KpiStripPanelId, 0, 0, OccLayoutGridConstants.FullWidthSpan, 1, 72),
        Panel(OccLayoutDefaults.ImmediateLanePanelId, 0, 1, 5, 1, 320),
        Panel(OccLayoutDefaults.KanbanPanelId, 5, 1, 7, 1, 360),
        Panel(OccLayoutDefaults.BranchMetricsPanelId, 0, 2, OccLayoutGridConstants.FullWidthSpan, 1, 80),
        HiddenPanel(OccLayoutDefaults.AnalyticsPanelId),
        HiddenPanel(OccLayoutDefaults.PlatformIntelligencePanelId),
        HiddenPanel(OccLayoutDefaults.HighlightsPanelId),
        HiddenPanel(OccLayoutDefaults.AiFeedPanelId),
        HiddenPanel(OccLayoutDefaults.DataHealthPanelId),
        HiddenPanel(OccLayoutDefaults.BranchPulsePanelId)
    ];

    private static int ResolveDefaultMinHeight(string panelId) => panelId switch
    {
        OccLayoutDefaults.KpiStripPanelId => 72,
        OccLayoutDefaults.ImmediateLanePanelId => 200,
        OccLayoutDefaults.KanbanPanelId => 280,
        OccLayoutDefaults.BranchMetricsPanelId => 100,
        OccLayoutDefaults.BranchPulsePanelId => 160,
        OccLayoutDefaults.HighlightsPanelId => 120,
        OccLayoutDefaults.AiFeedPanelId => 160,
        OccLayoutDefaults.PlatformIntelligencePanelId => 120,
        OccLayoutDefaults.AnalyticsPanelId => 160,
        OccLayoutDefaults.DataHealthPanelId => 80,
        _ => 80
    };

    private static OccPanelPlacement Panel(
        string panelId,
        int column,
        int row,
        int columnSpan,
        int rowSpan,
        int minHeightDp = 0) => new()
    {
        PanelId = panelId,
        Column = column,
        Row = row,
        ColumnSpan = columnSpan,
        RowSpan = rowSpan,
        IsVisible = true,
        MinHeightDp = minHeightDp > 0 ? minHeightDp : ResolveDefaultMinHeight(panelId)
    };

    private static OccPanelPlacement HiddenPanel(string panelId) => new()
    {
        PanelId = panelId,
        IsVisible = false,
        MinHeightDp = ResolveDefaultMinHeight(panelId)
    };
}
