using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OccLayoutServiceTests
{
    [Fact]
    public void Resolve_RejectsUnknownPanelIds()
    {
        var settings = new AppSettings
        {
            OccActionPanelOrder = ["Kanban", "UnknownPanel", "ImmediateLane"],
            OccContextPanelOrder = ["AiFeed", "NotReal", "Highlights"],
            OccKpiMetricOrder = ["sla", "bogus", "open"],
            OccHiddenPanels = ["AiFeed", "FakePanel"]
        };

        var layout = OccLayoutService.Resolve(settings);

        Assert.Equal(
            [OccLayoutDefaults.KanbanPanelId, OccLayoutDefaults.ImmediateLanePanelId],
            layout.ActionPanelOrder);
        Assert.Contains(OccLayoutDefaults.AiFeedPanelId, layout.ContextPanelOrder);
        Assert.Contains(OccLayoutDefaults.HighlightsPanelId, layout.ContextPanelOrder);
        Assert.Equal("sla", layout.KpiMetricOrder[0]);
        Assert.Contains("open", layout.KpiMetricOrder);
        Assert.Equal(OccLayoutDefaults.KpiMetricOrder.Count, layout.KpiMetricOrder.Count);
        Assert.DoesNotContain("FakePanel", layout.HiddenPanels);
        Assert.Contains(OccLayoutDefaults.AiFeedPanelId, layout.HiddenPanels);
    }

    [Fact]
    public void ApplyDefaults_RestoresFactoryLayout()
    {
        var settings = new AppSettings
        {
            OccActionPanelOrder = [OccLayoutDefaults.KanbanPanelId, OccLayoutDefaults.ImmediateLanePanelId],
            OccContextPanelOrder = [OccLayoutDefaults.AnalyticsPanelId],
            OccKpiMetricOrder = ["sla", "open", "hanging", "revenue", "immediate"],
            OccHiddenPanels = [OccLayoutDefaults.HighlightsPanelId]
        };

        OccLayoutService.ApplyDefaults(settings);
        OccLayoutService.Normalize(settings);

        Assert.Equal(OccLayoutDefaults.ActionPanelOrder, settings.OccActionPanelOrder);
        Assert.Equal(OccLayoutDefaults.ContextPanelOrder, settings.OccContextPanelOrder);
        Assert.Equal(OccLayoutDefaults.KpiMetricOrder, settings.OccKpiMetricOrder);
        Assert.Empty(settings.OccHiddenPanels);
    }
}
