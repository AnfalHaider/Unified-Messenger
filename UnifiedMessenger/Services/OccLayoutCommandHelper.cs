using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class OccLayoutCommandHelper
{
    public static OccDashboardLayoutPreferences CaptureCurrentLayout(AppSettings settings) =>
        OccLayoutService.Resolve(settings);

    public static void ApplyLayoutSnapshot(AppSettings settings, OccDashboardLayoutPreferences snapshot)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(snapshot);

        settings.OccActionPanelOrder = snapshot.ActionPanelOrder.ToList();
        settings.OccContextPanelOrder = snapshot.ContextPanelOrder.ToList();
        settings.OccKpiMetricOrder = snapshot.KpiMetricOrder.ToList();
        settings.OccHiddenPanels = snapshot.HiddenPanels.ToList();
        settings.OccPanelPlacements = snapshot.PanelPlacements
            .Select(placement => placement.Clone())
            .ToList();
        settings.OccLayoutPresetId = snapshot.LayoutPresetId;
    }

    public static void RestoreDefaults(AppSettings settings) =>
        OccLayoutService.ApplyDefaults(settings);

    public static void MovePanel(
        AppSettings settings,
        IReadOnlyList<string> currentOrder,
        string sourcePanelId,
        string targetPanelId,
        Action<AppSettings, List<string>> persistOrder)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(currentOrder);
        ArgumentNullException.ThrowIfNull(persistOrder);

        if (string.IsNullOrWhiteSpace(sourcePanelId) ||
            string.IsNullOrWhiteSpace(targetPanelId) ||
            sourcePanelId.Equals(targetPanelId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var order = currentOrder.ToList();
        var sourceIndex = order.FindIndex(id => id.Equals(sourcePanelId, StringComparison.OrdinalIgnoreCase));
        var targetIndex = order.FindIndex(id => id.Equals(targetPanelId, StringComparison.OrdinalIgnoreCase));
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        order.RemoveAt(sourceIndex);
        order.Insert(targetIndex, sourcePanelId);
        persistOrder(settings, order);
    }
}
