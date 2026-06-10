using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Loads, validates, and persists Operations Command Center dashboard layout preferences.
/// </summary>
public static class OccLayoutService
{
    public static readonly IReadOnlyList<string> DefaultActionPanelOrder = OccLayoutDefaults.ActionPanelOrder;

    public static readonly IReadOnlyList<string> DefaultContextPanelOrder = OccLayoutDefaults.ContextPanelOrder;

    public static readonly IReadOnlyList<string> DefaultKpiMetricOrder = OccLayoutDefaults.KpiMetricOrder;

    private static readonly HashSet<string> AllowedActionPanels =
        new(DefaultActionPanelOrder, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedContextPanels =
        new(DefaultContextPanelOrder, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedKpiMetrics =
        new(DefaultKpiMetricOrder, StringComparer.OrdinalIgnoreCase);

    public static OccDashboardLayoutPreferences Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var placements = OccLayoutGridEngine.Resolve(settings);
        SyncLegacyFromPlacements(settings, placements);

        return new OccDashboardLayoutPreferences
        {
            ActionPanelOrder = settings.OccActionPanelOrder,
            ContextPanelOrder = settings.OccContextPanelOrder,
            KpiMetricOrder = SanitizePanelOrder(settings.OccKpiMetricOrder, AllowedKpiMetrics, DefaultKpiMetricOrder),
            HiddenPanels = settings.OccHiddenPanels,
            PanelPlacements = placements,
            LayoutPresetId = string.IsNullOrWhiteSpace(settings.OccLayoutPresetId)
                ? OccLayoutPresets.OperationsFocus
                : settings.OccLayoutPresetId.Trim()
        };
    }

    public static void ApplyDefaults(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.OccLayoutPresetId = OccLayoutPresets.OperationsFocus;
        settings.OccPanelPlacements = OccLayoutPresets.CreateOperationsFocus()
            .Select(placement => placement.Clone())
            .ToList();
        settings.OccActionPanelOrder = DefaultActionPanelOrder.ToList();
        settings.OccContextPanelOrder = DefaultContextPanelOrder.ToList();
        settings.OccKpiMetricOrder = DefaultKpiMetricOrder.ToList();
        settings.OccHiddenPanels = [];
    }

    public static void ApplyPreset(AppSettings settings, string presetId)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedPreset = OccLayoutPresets.All.Contains(presetId, StringComparer.OrdinalIgnoreCase)
            ? presetId
            : OccLayoutPresets.OperationsFocus;

        settings.OccLayoutPresetId = normalizedPreset;
        settings.OccPanelPlacements = OccLayoutPresets.Create(normalizedPreset)
            .Select(placement => placement.Clone())
            .ToList();
        SyncLegacyFromPlacements(settings, settings.OccPanelPlacements);
    }

    public static void Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.OccPanelPlacements is null || settings.OccPanelPlacements.Count == 0)
        {
            settings.OccPanelPlacements = OccLayoutGridEngine
                .MigrateFromLegacy(
                    settings.OccActionPanelOrder,
                    settings.OccContextPanelOrder,
                    settings.OccHiddenPanels)
                .Select(placement => placement.Clone())
                .ToList();
        }

        var resolved = OccLayoutGridEngine.Resolve(settings);
        settings.OccPanelPlacements = resolved.Select(placement => placement.Clone()).ToList();
        SyncLegacyFromPlacements(settings, resolved);

        settings.OccActionPanelOrder = SanitizePanelOrder(
            settings.OccActionPanelOrder,
            AllowedActionPanels,
            DefaultActionPanelOrder).ToList();
        settings.OccContextPanelOrder = SanitizePanelOrder(
            settings.OccContextPanelOrder,
            AllowedContextPanels,
            DefaultContextPanelOrder).ToList();
        settings.OccKpiMetricOrder = SanitizePanelOrder(
            settings.OccKpiMetricOrder,
            AllowedKpiMetrics,
            DefaultKpiMetricOrder).ToList();
        settings.OccHiddenPanels = SanitizeHiddenPanels(settings.OccHiddenPanels).ToList();

        if (string.IsNullOrWhiteSpace(settings.OccLayoutPresetId))
        {
            settings.OccLayoutPresetId = OccLayoutPresets.OperationsFocus;
        }
    }

    public static void PersistPlacements(AppSettings settings, IReadOnlyList<OccPanelPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(placements);

        settings.OccPanelPlacements = placements.Select(placement => placement.Clone()).ToList();
        settings.OccLayoutPresetId = string.Empty;
    }

    internal static void SyncLegacyFromPlacements(
        AppSettings settings,
        IReadOnlyList<OccPanelPlacement> placements)
    {
        var action = placements
            .Where(placement => AllowedActionPanels.Contains(placement.PanelId))
            .OrderBy(placement => placement.Row)
            .ThenBy(placement => placement.Column)
            .Select(placement => placement.PanelId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var context = placements
            .Where(placement => AllowedContextPanels.Contains(placement.PanelId))
            .OrderBy(placement => placement.Row)
            .ThenBy(placement => placement.Column)
            .Select(placement => placement.PanelId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.OccActionPanelOrder = SanitizePanelOrder(action, AllowedActionPanels, DefaultActionPanelOrder).ToList();
        settings.OccContextPanelOrder = SanitizePanelOrder(context, AllowedContextPanels, DefaultContextPanelOrder).ToList();
        settings.OccHiddenPanels = placements
            .Where(placement => !placement.IsVisible)
            .Select(placement => placement.PanelId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> SanitizePanelOrder(
        IReadOnlyList<string>? stored,
        IReadOnlySet<string> allowed,
        IReadOnlyList<string> defaults)
    {
        var results = new List<string>();
        if (stored is not null)
        {
            foreach (var panelId in stored)
            {
                if (string.IsNullOrWhiteSpace(panelId))
                {
                    continue;
                }

                var normalized = panelId.Trim();
                if (allowed.Contains(normalized) && !results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(normalized);
                }
            }
        }

        foreach (var panelId in defaults)
        {
            if (!results.Contains(panelId, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(panelId);
            }
        }

        return results;
    }

    private static IReadOnlyList<string> SanitizeHiddenPanels(IReadOnlyList<string>? stored)
    {
        if (stored is null || stored.Count == 0)
        {
            return [];
        }

        var allPanels = AllowedActionPanels
            .Concat(AllowedContextPanels)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return stored
            .Where(panelId => !string.IsNullOrWhiteSpace(panelId) && allPanels.Contains(panelId.Trim()))
            .Select(panelId => panelId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class OccDashboardLayoutPreferences
{
    public IReadOnlyList<string> ActionPanelOrder { get; init; } = OccLayoutDefaults.ActionPanelOrder;

    public IReadOnlyList<string> ContextPanelOrder { get; init; } = OccLayoutDefaults.ContextPanelOrder;

    public IReadOnlyList<string> KpiMetricOrder { get; init; } = OccLayoutDefaults.KpiMetricOrder;

    public IReadOnlyList<string> HiddenPanels { get; init; } = [];

    public IReadOnlyList<OccPanelPlacement> PanelPlacements { get; init; } =
        OccLayoutPresets.CreateOperationsFocus();

    public string LayoutPresetId { get; init; } = OccLayoutPresets.OperationsFocus;
}
