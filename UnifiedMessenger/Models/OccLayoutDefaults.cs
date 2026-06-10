namespace UnifiedMessenger.Models;

public static class OccLayoutDefaults
{
    public const string KpiStripPanelId = "KpiStrip";

    public const string ImmediateLanePanelId = "ImmediateLane";

    public const string KanbanPanelId = "Kanban";

    public const string BranchMetricsPanelId = "BranchMetrics";

    public const string HighlightsPanelId = "Highlights";

    public const string AiFeedPanelId = "AiFeed";

    public const string PlatformIntelligencePanelId = "PlatformIntelligence";

    public const string AnalyticsPanelId = "Analytics";

    public const string DataHealthPanelId = "DataHealth";

    public static readonly IReadOnlyList<string> ActionPanelOrder =
        [ImmediateLanePanelId, KanbanPanelId];

    public static readonly IReadOnlyList<string> ContextPanelOrder =
    [
        BranchMetricsPanelId,
        HighlightsPanelId,
        AiFeedPanelId,
        PlatformIntelligencePanelId,
        AnalyticsPanelId,
        DataHealthPanelId
    ];

    public static readonly IReadOnlyList<string> KpiMetricOrder =
        ["open", "hanging", "revenue", "immediate", "sla"];
}
