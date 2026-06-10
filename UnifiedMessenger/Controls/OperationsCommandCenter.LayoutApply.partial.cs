using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void ApplyLayoutPreferences()
    {
        var layout = OccLayoutService.Resolve(_services.AppSettings.Settings);
        ApplyGridLayout(layout);
        ApplyKpiLayout(layout.KpiMetricOrder);
        ApplyCompactPresetChrome(layout.LayoutPresetId);
        UpdateHiddenPanelsTray(layout.PanelPlacements);
    }

    private void ApplyGridLayout(OccDashboardLayoutPreferences layout)
    {
        var width = ResolveOccLayoutWidth();
        var placements = OccLayoutGridEngine.ReflowForWidth(layout.PanelPlacements, width);
        OccLayoutGridApplier.Apply(OccLayoutGrid, placements, BuildPanelMap());
    }

    private double ResolveOccLayoutWidth()
    {
        if (OccLayoutGrid.ActualWidth > 0)
        {
            return OccLayoutGrid.ActualWidth;
        }

        return ActualWidth > 0 ? ActualWidth : OccLayoutGridConstants.BreakpointWide;
    }

    private void OccLayoutGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 1)
        {
            return;
        }

        var layout = OccLayoutService.Resolve(_services.AppSettings.Settings);
        ApplyGridLayout(layout);
    }

    private void ApplyCompactPresetChrome(string presetId)
    {
        if (!presetId.Equals(OccLayoutPresets.Compact, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PlatformIntelligenceExpander.IsExpanded = false;
        AnalyticsTrendsExpander.IsExpanded = false;
    }

    private void ApplyKpiLayout(IReadOnlyList<string> metricOrder)
    {
        var cardMap = BuildKpiCardMap();
        for (var index = 0; index < metricOrder.Count; index++)
        {
            if (!cardMap.TryGetValue(metricOrder[index], out var card))
            {
                continue;
            }

            Grid.SetRow(card, 1);
            Grid.SetColumn(card, index);
            Grid.SetColumnSpan(card, 1);
        }
    }

    private Dictionary<string, FrameworkElement> BuildPanelMap()
    {
        var panelMap = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
        {
            [OccLayoutDefaults.KpiStripPanelId] = KpiStripSection,
            [OccLayoutDefaults.ImmediateLanePanelId] = ImmediateLaneSection,
            [OccLayoutDefaults.KanbanPanelId] = KanbanSection,
            [OccLayoutDefaults.BranchMetricsPanelId] = BranchMetricsSection,
            [OccLayoutDefaults.BranchPulsePanelId] = BranchPulseSection,
            [OccLayoutDefaults.HighlightsPanelId] = HighlightsSection,
            [OccLayoutDefaults.AiFeedPanelId] = AiFeedSection,
            [OccLayoutDefaults.PlatformIntelligencePanelId] = PlatformIntelligenceExpander,
            [OccLayoutDefaults.AnalyticsPanelId] = AnalyticsTrendsExpander,
            [OccLayoutDefaults.DataHealthPanelId] = HealthChipsSection
        };

        foreach (var (panelId, chrome) in _panelChromes)
        {
            panelMap[panelId] = chrome;
        }

        return panelMap;
    }

    private Dictionary<string, FrameworkElement> BuildKpiCardMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = OpenKpiCard,
        ["hanging"] = HangingKpiCard,
        ["revenue"] = RevenueKpiCard,
        ["immediate"] = ImmediateActionKpiCard,
        ["sla"] = SlaBreachesKpiCard
    };

    private IEnumerable<FrameworkElement> EnumerateLayoutSections()
    {
        foreach (var (panelId, section) in EnumerateLayoutSectionEntries())
        {
            yield return ResolveLayoutSectionSurface(panelId, section);
        }
    }

    private IEnumerable<(string PanelId, FrameworkElement Section)> EnumerateLayoutSectionEntries()
    {
        yield return (OccLayoutDefaults.KpiStripPanelId, KpiStripSection);
        yield return (OccLayoutDefaults.ImmediateLanePanelId, ImmediateLaneSection);
        yield return (OccLayoutDefaults.KanbanPanelId, KanbanSection);
        yield return (OccLayoutDefaults.BranchMetricsPanelId, BranchMetricsSection);
        yield return (OccLayoutDefaults.BranchPulsePanelId, BranchPulseSection);
        yield return (OccLayoutDefaults.HighlightsPanelId, HighlightsSection);
        yield return (OccLayoutDefaults.AiFeedPanelId, AiFeedSection);
        yield return (OccLayoutDefaults.PlatformIntelligencePanelId, PlatformIntelligenceExpander);
        yield return (OccLayoutDefaults.AnalyticsPanelId, AnalyticsTrendsExpander);
        yield return (OccLayoutDefaults.DataHealthPanelId, HealthChipsSection);
    }

    private FrameworkElement ResolveLayoutSectionSurface(string panelId, FrameworkElement section) =>
        _panelChromes.TryGetValue(panelId, out var chrome) ? chrome : section;

    private static string? ResolvePanelId(FrameworkElement element) =>
        element.Tag as string ?? (element is OccPanelChrome chrome ? chrome.PanelId : null);

    private IEnumerable<FrameworkElement> EnumerateKpiCards()
    {
        yield return OpenKpiCard;
        yield return HangingKpiCard;
        yield return RevenueKpiCard;
        yield return ImmediateActionKpiCard;
        yield return SlaBreachesKpiCard;
    }

    private void UpdateHiddenPanelsTray(IReadOnlyList<OccPanelPlacement> placements)
    {
        HiddenPanelsTray.Children.Clear();
        if (!_viewModel.IsLayoutEditMode)
        {
            return;
        }

        foreach (var placement in placements.Where(candidate => !candidate.IsVisible))
        {
            var restoreButton = new Button
            {
                Content = $"Show {ResolvePanelTitle(placement.PanelId)}",
                Tag = placement.PanelId,
                Style = Application.Current.Resources["SubtleButtonStyle"] as Style
            };
            restoreButton.Click += HiddenPanelRestoreButton_Click;
            HiddenPanelsTray.Children.Add(restoreButton);
        }
    }

    private static string ResolvePanelTitle(string panelId) => panelId switch
    {
        OccLayoutDefaults.KpiStripPanelId => "KPI strip",
        OccLayoutDefaults.ImmediateLanePanelId => "Immediate lane",
        OccLayoutDefaults.KanbanPanelId => "Kanban",
        OccLayoutDefaults.BranchMetricsPanelId => "Branch metrics",
        OccLayoutDefaults.BranchPulsePanelId => "Branch pulse",
        OccLayoutDefaults.HighlightsPanelId => "Highlights",
        OccLayoutDefaults.AiFeedPanelId => "AI feed",
        OccLayoutDefaults.PlatformIntelligencePanelId => "Platform intelligence",
        OccLayoutDefaults.AnalyticsPanelId => "Analytics",
        OccLayoutDefaults.DataHealthPanelId => "Data health",
        _ => panelId
    };

    private async void HiddenPanelRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string panelId)
        {
            return;
        }

        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var placements = OccLayoutService.Resolve(settings).PanelPlacements.ToList();
            var index = placements.FindIndex(placement =>
                placement.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return;
            }

            placements[index].IsVisible = true;
            OccLayoutService.PersistPlacements(settings, placements);
        }).ConfigureAwait(true);

        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Restored {ResolvePanelTitle(panelId)} panel.");
        ShowLayoutUndoInfoBar();
    }
}
