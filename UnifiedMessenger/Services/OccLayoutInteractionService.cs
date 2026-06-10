using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Non-UI OCC layout edit interactions (undo snapshots, keyboard nudges).
/// </summary>
public sealed class OccLayoutInteractionService
{
    public OccDashboardLayoutPreferences CloneSnapshot(OccDashboardLayoutPreferences current) =>
        new()
        {
            ActionPanelOrder = current.ActionPanelOrder.ToList(),
            ContextPanelOrder = current.ContextPanelOrder.ToList(),
            KpiMetricOrder = current.KpiMetricOrder.ToList(),
            HiddenPanels = current.HiddenPanels.ToList(),
            PanelPlacements = current.PanelPlacements.Select(placement => placement.Clone()).ToList(),
            LayoutPresetId = current.LayoutPresetId
        };

    public bool TryNudgePanel(
        IReadOnlyList<OccPanelPlacement> placements,
        string panelId,
        int deltaColumns,
        out IReadOnlyList<OccPanelPlacement> updated)
    {
        updated = placements;
        var current = placements.FirstOrDefault(placement =>
            placement.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            return false;
        }

        var targetColumn = Math.Clamp(
            current.Column + deltaColumns,
            0,
            OccLayoutGridConstants.GridColumns - current.ColumnSpan);

        return OccLayoutGridEngine.TryMove(
            placements,
            panelId,
            targetColumn,
            current.Row,
            out updated);
    }

    public bool TryResizePanel(
        IReadOnlyList<OccPanelPlacement> placements,
        string panelId,
        int deltaColumns,
        out IReadOnlyList<OccPanelPlacement> updated)
    {
        updated = placements;
        var current = placements.FirstOrDefault(placement =>
            placement.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            return false;
        }

        return OccLayoutGridEngine.TryResize(
            placements,
            panelId,
            current.ColumnSpan + deltaColumns,
            current.RowSpan,
            out updated);
    }
}
