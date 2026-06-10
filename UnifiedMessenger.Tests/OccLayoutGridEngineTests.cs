using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.System;
using Xunit;

namespace UnifiedMessenger.Tests;

public class OccLayoutGridEngineTests
{
    [Fact]
    public void Resolve_MigratesLegacyTwoColumnOrder()
    {
        var settings = new AppSettings
        {
            OccActionPanelOrder = [OccLayoutDefaults.KanbanPanelId, OccLayoutDefaults.ImmediateLanePanelId],
            OccContextPanelOrder =
            [
                OccLayoutDefaults.AnalyticsPanelId,
                OccLayoutDefaults.HighlightsPanelId
            ],
            OccHiddenPanels = [OccLayoutDefaults.AiFeedPanelId]
        };

        var placements = OccLayoutGridEngine.Resolve(settings);

        Assert.Contains(placements, placement =>
            placement.PanelId == OccLayoutDefaults.KpiStripPanelId &&
            placement.Column == OccLayoutGridConstants.LeftColumn &&
            placement.ColumnSpan == OccLayoutGridConstants.FullWidthSpan &&
            placement.Row == 0);
        Assert.Contains(placements, placement =>
            placement.PanelId == OccLayoutDefaults.KanbanPanelId &&
            placement.Column == OccLayoutGridConstants.LeftColumn &&
            placement.Row == 1);
        Assert.Contains(placements, placement =>
            placement.PanelId == OccLayoutDefaults.ImmediateLanePanelId &&
            placement.Column == OccLayoutGridConstants.LeftColumn &&
            placement.Row == 2);
        Assert.Contains(placements, placement =>
            placement.PanelId == OccLayoutDefaults.AnalyticsPanelId &&
            placement.Column == OccLayoutGridConstants.RightColumn);
        Assert.All(
            placements.Where(placement => placement.PanelId == OccLayoutDefaults.AiFeedPanelId),
            placement => Assert.False(placement.IsVisible));
    }

    [Fact]
    public void TryMove_AllowsCrossColumnPlacement()
    {
        var placements = OccLayoutPresets.CreateOperationsFocus();
        var moved = OccLayoutGridEngine.TryMove(
            placements,
            OccLayoutDefaults.KanbanPanelId,
            0,
            2,
            out var updated);

        Assert.True(moved);
        Assert.Contains(
            updated,
            placement => placement.PanelId == OccLayoutDefaults.KanbanPanelId &&
                         placement.Column == 0 &&
                         placement.Row == 2);
    }

    [Fact]
    public void TryResize_EnforcesMinimumColumnSpan()
    {
        var placements = OccLayoutPresets.CreateOperationsFocus();
        var resized = OccLayoutGridEngine.TryResize(
            placements,
            OccLayoutDefaults.KanbanPanelId,
            2,
            1,
            out var updated);

        Assert.True(resized);
        var kanban = updated.Single(placement => placement.PanelId == OccLayoutDefaults.KanbanPanelId);
        Assert.Equal(OccLayoutGridEngine.ResolveMinColumnSpan(OccLayoutDefaults.KanbanPanelId), kanban.ColumnSpan);
    }

    [Fact]
    public void ApplyPreset_AnalyticsFocus_PrioritizesAnalyticsPanels()
    {
        var placements = OccLayoutGridEngine.ApplyPreset(OccLayoutPresets.AnalyticsFocus);
        var analytics = placements.Single(placement => placement.PanelId == OccLayoutDefaults.AnalyticsPanelId);

        Assert.True(analytics.ColumnSpan >= 6);
        Assert.True(analytics.Row <= 3);
    }

    [Fact]
    public void ApplyPreset_OperationsFocus_IncludesKpiStripAtRowZero()
    {
        var placements = OccLayoutGridEngine.ApplyPreset(OccLayoutPresets.OperationsFocus);
        var kpi = placements.Single(placement => placement.PanelId == OccLayoutDefaults.KpiStripPanelId);

        Assert.Equal(0, kpi.Row);
        Assert.Equal(OccLayoutGridConstants.FullWidthSpan, kpi.ColumnSpan);
    }

    [Fact]
    public void ApplyPreset_Compact_IncludesKpiStripFirst()
    {
        var placements = OccLayoutGridEngine.ApplyPreset(OccLayoutPresets.Compact);

        Assert.Equal(OccLayoutDefaults.KpiStripPanelId, placements[0].PanelId);
        Assert.Equal(0, placements[0].Row);
    }

    [Fact]
    public void ReflowForWidth_BelowNarrowBreakpoint_StacksSingleColumn()
    {
        var placements = OccLayoutGridEngine.ApplyPreset(OccLayoutPresets.OperationsFocus);
        var reflowed = OccLayoutGridEngine.ReflowForWidth(placements, 360);

        Assert.All(
            reflowed.Where(placement => placement.IsVisible),
            placement =>
            {
                Assert.Equal(0, placement.Column);
                Assert.Equal(OccLayoutGridConstants.FullWidthSpan, placement.ColumnSpan);
            });
        Assert.Equal(0, reflowed.Single(p => p.PanelId == OccLayoutDefaults.KpiStripPanelId).Row);
    }

    [Fact]
    public void ReflowForWidth_AboveWideBreakpoint_PreservesLayout()
    {
        var placements = OccLayoutGridEngine.ApplyPreset(OccLayoutPresets.OperationsFocus);
        var reflowed = OccLayoutGridEngine.ReflowForWidth(placements, 1200);

        var kanban = reflowed.Single(placement => placement.PanelId == OccLayoutDefaults.KanbanPanelId);
        var sourceKanban = placements.Single(placement => placement.PanelId == OccLayoutDefaults.KanbanPanelId);
        Assert.Equal(sourceKanban.Column, kanban.Column);
        Assert.Equal(sourceKanban.ColumnSpan, kanban.ColumnSpan);
        Assert.Equal(sourceKanban.Row, kanban.Row);
    }

    [Fact]
    public void ReflowForWidth_MediumBreakpoint_ReducesColumnSpans()
    {
        var placements = OccLayoutGridEngine.ApplyPreset(OccLayoutPresets.OperationsFocus);
        var reflowed = OccLayoutGridEngine.ReflowForWidth(placements, 800);
        var kanban = reflowed.Single(placement => placement.PanelId == OccLayoutDefaults.KanbanPanelId);

        Assert.True(kanban.ColumnSpan < placements.Single(p => p.PanelId == OccLayoutDefaults.KanbanPanelId).ColumnSpan);
    }

    [Fact]
    public void ResolveMinColumnSpan_KpiStrip_IsFullWidth()
    {
        Assert.Equal(
            OccLayoutGridConstants.FullWidthSpan,
            OccLayoutGridEngine.ResolveMinColumnSpan(OccLayoutDefaults.KpiStripPanelId));
    }

    [Fact]
    public void SetVisibility_HidesAndRestoresPanel()
    {
        var placements = OccLayoutPresets.CreateOperationsFocus();
        var hidden = OccLayoutGridEngine.SetVisibility(
            placements,
            OccLayoutDefaults.HighlightsPanelId,
            false);

        Assert.Contains(
            hidden,
            placement => placement.PanelId == OccLayoutDefaults.HighlightsPanelId && !placement.IsVisible);

        var restored = OccLayoutGridEngine.SetVisibility(
            hidden,
            OccLayoutDefaults.HighlightsPanelId,
            true);

        Assert.Contains(
            restored,
            placement => placement.PanelId == OccLayoutDefaults.HighlightsPanelId && placement.IsVisible);
    }

    [Fact]
    public void OccLayoutService_ApplyDefaults_SetsGridPlacements()
    {
        var settings = new AppSettings();
        OccLayoutService.ApplyDefaults(settings);

        Assert.NotEmpty(settings.OccPanelPlacements);
        Assert.Contains(
            settings.OccPanelPlacements,
            placement => placement.PanelId == OccLayoutDefaults.KpiStripPanelId);
        Assert.Equal(OccLayoutPresets.OperationsFocus, settings.OccLayoutPresetId);
    }

    [Fact]
    public void OccLayoutCommandHelper_ApplyLayoutSnapshot_RestoresCapturedLayout()
    {
        var settings = new AppSettings();
        OccLayoutService.ApplyDefaults(settings);
        var snapshot = OccLayoutCommandHelper.CaptureCurrentLayout(settings);

        var modified = OccLayoutGridEngine.SetVisibility(
            snapshot.PanelPlacements,
            OccLayoutDefaults.HighlightsPanelId,
            false);
        settings.OccPanelPlacements = modified.ToList();
        settings.OccHiddenPanels = [OccLayoutDefaults.HighlightsPanelId];

        OccLayoutCommandHelper.ApplyLayoutSnapshot(settings, snapshot);

        var restored = OccLayoutService.Resolve(settings);
        Assert.DoesNotContain(
            restored.HiddenPanels,
            panelId => panelId.Equals(OccLayoutDefaults.HighlightsPanelId, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            restored.PanelPlacements,
            placement =>
                placement.PanelId == OccLayoutDefaults.HighlightsPanelId && placement.IsVisible);
    }

    [Fact]
    public void OperationsCommandCenter_IsLayoutUndoShortcut_RequiresControlZ()
    {
        Assert.False(OperationsCommandCenter.IsLayoutUndoShortcut(VirtualKey.Z, isControlDown: false));
        Assert.False(OperationsCommandCenter.IsLayoutUndoShortcut(VirtualKey.Y, isControlDown: true));
        Assert.True(OperationsCommandCenter.IsLayoutUndoShortcut(VirtualKey.Z, isControlDown: true));
    }
}
