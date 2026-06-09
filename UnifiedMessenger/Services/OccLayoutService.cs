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

        return new OccDashboardLayoutPreferences
        {
            ActionPanelOrder = SanitizePanelOrder(settings.OccActionPanelOrder, AllowedActionPanels, DefaultActionPanelOrder),
            ContextPanelOrder = SanitizePanelOrder(settings.OccContextPanelOrder, AllowedContextPanels, DefaultContextPanelOrder),
            KpiMetricOrder = SanitizePanelOrder(settings.OccKpiMetricOrder, AllowedKpiMetrics, DefaultKpiMetricOrder),
            HiddenPanels = SanitizeHiddenPanels(settings.OccHiddenPanels)
        };
    }

    public static void ApplyDefaults(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.OccActionPanelOrder = DefaultActionPanelOrder.ToList();
        settings.OccContextPanelOrder = DefaultContextPanelOrder.ToList();
        settings.OccKpiMetricOrder = DefaultKpiMetricOrder.ToList();
        settings.OccHiddenPanels = [];
    }

    public static void Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var resolved = Resolve(settings);
        settings.OccActionPanelOrder = resolved.ActionPanelOrder.ToList();
        settings.OccContextPanelOrder = resolved.ContextPanelOrder.ToList();
        settings.OccKpiMetricOrder = resolved.KpiMetricOrder.ToList();
        settings.OccHiddenPanels = resolved.HiddenPanels.ToList();
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
}
