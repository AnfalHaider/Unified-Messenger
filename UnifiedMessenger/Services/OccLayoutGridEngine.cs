using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Pure grid layout engine for Operations Command Center panel placements.
/// </summary>
public static class OccLayoutGridEngine
{
    private static readonly HashSet<string> AllowedPanels = new(
        new[] { OccLayoutDefaults.KpiStripPanelId }
            .Concat(OccLayoutDefaults.ActionPanelOrder)
            .Concat(OccLayoutDefaults.ContextPanelOrder),
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, int> MinColumnSpans =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [OccLayoutDefaults.KpiStripPanelId] = OccLayoutGridConstants.FullWidthSpan,
            [OccLayoutDefaults.ImmediateLanePanelId] = 4,
            [OccLayoutDefaults.KanbanPanelId] = 6,
            [OccLayoutDefaults.BranchMetricsPanelId] = 6,
            [OccLayoutDefaults.HighlightsPanelId] = 3,
            [OccLayoutDefaults.AiFeedPanelId] = 3,
            [OccLayoutDefaults.PlatformIntelligencePanelId] = 4,
            [OccLayoutDefaults.AnalyticsPanelId] = 4,
            [OccLayoutDefaults.DataHealthPanelId] = 3
        };

    private static readonly IReadOnlyDictionary<string, int> MinHeightsDp =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [OccLayoutDefaults.KpiStripPanelId] = 72,
            [OccLayoutDefaults.ImmediateLanePanelId] = 200,
            [OccLayoutDefaults.KanbanPanelId] = 280,
            [OccLayoutDefaults.BranchMetricsPanelId] = 100,
            [OccLayoutDefaults.HighlightsPanelId] = 120,
            [OccLayoutDefaults.AiFeedPanelId] = 160,
            [OccLayoutDefaults.PlatformIntelligencePanelId] = 120,
            [OccLayoutDefaults.AnalyticsPanelId] = 160,
            [OccLayoutDefaults.DataHealthPanelId] = 80
        };

    public static int ResolveMinColumnSpan(string panelId) =>
        MinColumnSpans.TryGetValue(panelId, out var span) ? span : 3;

    public static int ResolveMinHeightDp(string panelId) =>
        MinHeightsDp.TryGetValue(panelId, out var height) ? height : 80;

    public static IReadOnlyList<OccPanelPlacement> Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (settings.OccHiddenPanels is not null)
        {
            foreach (var panelId in settings.OccHiddenPanels)
            {
                if (!string.IsNullOrWhiteSpace(panelId))
                {
                    hidden.Add(panelId.Trim());
                }
            }
        }

        IReadOnlyList<OccPanelPlacement> source;
        if (settings.OccPanelPlacements is { Count: > 0 })
        {
            source = settings.OccPanelPlacements;
        }
        else
        {
            source = MigrateFromLegacy(
                settings.OccActionPanelOrder,
                settings.OccContextPanelOrder,
                settings.OccHiddenPanels);
        }

        var resolved = SanitizePlacements(source, hidden);
        return ResolveCollisions(resolved);
    }

    public static IReadOnlyList<OccPanelPlacement> ApplyPreset(string presetId) =>
        SanitizePlacements(OccLayoutPresets.Create(presetId), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<OccPanelPlacement> MigrateFromLegacy(
        IReadOnlyList<string>? actionOrder,
        IReadOnlyList<string>? contextOrder,
        IReadOnlyList<string>? hiddenPanels)
    {
        var hidden = new HashSet<string>(hiddenPanels ?? [], StringComparer.OrdinalIgnoreCase);
        var placements = new List<OccPanelPlacement>
        {
            CreatePlacement(
                OccLayoutDefaults.KpiStripPanelId,
                OccLayoutGridConstants.LeftColumn,
                0,
                OccLayoutGridConstants.FullWidthSpan,
                hidden.Contains(OccLayoutDefaults.KpiStripPanelId))
        };

        var action = SanitizeOrder(actionOrder, OccLayoutDefaults.ActionPanelOrder);
        for (var row = 0; row < action.Count; row++)
        {
            placements.Add(CreatePlacement(
                action[row],
                OccLayoutGridConstants.LeftColumn,
                row + 1,
                OccLayoutGridConstants.LeftColumnSpan,
                hidden.Contains(action[row])));
        }

        var context = SanitizeOrder(contextOrder, OccLayoutDefaults.ContextPanelOrder);
        for (var row = 0; row < context.Count; row++)
        {
            placements.Add(CreatePlacement(
                context[row],
                OccLayoutGridConstants.RightColumn,
                row + 1,
                OccLayoutGridConstants.RightColumnSpan,
                hidden.Contains(context[row])));
        }

        return placements;
    }

    public static bool TryMove(
        IReadOnlyList<OccPanelPlacement> placements,
        string panelId,
        int targetColumn,
        int targetRow,
        out IReadOnlyList<OccPanelPlacement> updated)
    {
        updated = placements;
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return false;
        }

        var working = placements.Select(placement => placement.Clone()).ToList();
        var index = working.FindIndex(placement =>
            placement.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        var moving = working[index];
        moving.Column = Math.Clamp(targetColumn, 0, OccLayoutGridConstants.GridColumns - 1);
        moving.Row = Math.Max(0, targetRow);
        working[index] = moving;
        updated = ResolveCollisions(working);
        return true;
    }

    public static bool TryResize(
        IReadOnlyList<OccPanelPlacement> placements,
        string panelId,
        int newColumnSpan,
        int newRowSpan,
        out IReadOnlyList<OccPanelPlacement> updated)
    {
        updated = placements;
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return false;
        }

        var working = placements.Select(placement => placement.Clone()).ToList();
        var index = working.FindIndex(placement =>
            placement.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        var minSpan = ResolveMinColumnSpan(panelId);
        var placement = working[index];
        placement.ColumnSpan = Math.Clamp(
            newColumnSpan,
            minSpan,
            OccLayoutGridConstants.GridColumns - placement.Column);
        placement.RowSpan = Math.Max(1, newRowSpan);
        working[index] = placement;
        updated = ResolveCollisions(working);
        return true;
    }

    /// <summary>
    /// Adjusts column spans and positions for responsive reflow at 960 / 600 / 400 dp breakpoints.
    /// Does not mutate stored placements; returns a view-model copy for UI application.
    /// </summary>
    public static IReadOnlyList<OccPanelPlacement> ReflowForWidth(
        IReadOnlyList<OccPanelPlacement> placements,
        double widthDp)
    {
        ArgumentNullException.ThrowIfNull(placements);

        var working = placements.Select(placement => placement.Clone()).ToList();
        if (widthDp <= 0 || widthDp >= OccLayoutGridConstants.BreakpointWide)
        {
            return working;
        }

        if (widthDp < OccLayoutGridConstants.BreakpointNarrow)
        {
            return ReflowSingleColumn(working);
        }

        var scale = widthDp < OccLayoutGridConstants.BreakpointMedium
            ? 0.67
            : widthDp / OccLayoutGridConstants.BreakpointWide;

        return ReflowScaled(working, scale);
    }

    public static IReadOnlyList<OccPanelPlacement> SetVisibility(
        IReadOnlyList<OccPanelPlacement> placements,
        string panelId,
        bool isVisible)
    {
        var working = placements.Select(placement => placement.Clone()).ToList();
        var index = working.FindIndex(placement =>
            placement.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return placements;
        }

        working[index].IsVisible = isVisible;
        return working;
    }

    internal static IReadOnlyList<OccPanelPlacement> SanitizePlacements(
        IReadOnlyList<OccPanelPlacement> source,
        IReadOnlyCollection<string> hiddenPanels)
    {
        var hidden = hiddenPanels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<OccPanelPlacement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var placement in source)
        {
            if (string.IsNullOrWhiteSpace(placement.PanelId) ||
                !AllowedPanels.Contains(placement.PanelId.Trim()) ||
                !seen.Add(placement.PanelId.Trim()))
            {
                continue;
            }

            var panelId = placement.PanelId.Trim();
            var minSpan = ResolveMinColumnSpan(panelId);
            var column = Math.Clamp(placement.Column, 0, OccLayoutGridConstants.GridColumns - 1);
            var columnSpan = Math.Clamp(
                placement.ColumnSpan,
                minSpan,
                OccLayoutGridConstants.GridColumns - column);

            results.Add(new OccPanelPlacement
            {
                PanelId = panelId,
                Column = column,
                Row = Math.Max(0, placement.Row),
                ColumnSpan = columnSpan,
                RowSpan = Math.Max(1, placement.RowSpan),
                IsVisible = placement.IsVisible && !hidden.Contains(panelId),
                MinHeightDp = placement.MinHeightDp > 0
                    ? placement.MinHeightDp
                    : ResolveMinHeightDp(panelId)
            });
        }

        foreach (var panelId in AllowedPanels)
        {
            if (seen.Contains(panelId))
            {
                continue;
            }

            if (panelId.Equals(OccLayoutDefaults.KpiStripPanelId, StringComparison.OrdinalIgnoreCase))
            {
                results.Insert(0, CreatePlacement(
                    panelId,
                    OccLayoutGridConstants.LeftColumn,
                    0,
                    OccLayoutGridConstants.FullWidthSpan,
                    hidden.Contains(panelId)));
                continue;
            }

            results.Add(CreatePlacement(
                panelId,
                OccLayoutGridConstants.LeftColumn,
                results.Count,
                OccLayoutGridConstants.DefaultColumnSpan,
                hidden.Contains(panelId)));
        }

        return results;
    }

    internal static IReadOnlyList<OccPanelPlacement> ResolveCollisions(IReadOnlyList<OccPanelPlacement> placements)
    {
        var ordered = placements
            .OrderBy(placement => placement.Row)
            .ThenBy(placement => placement.Column)
            .Select(placement => placement.Clone())
            .ToList();

        var occupied = new HashSet<(int Row, int Col)>();
        foreach (var placement in ordered)
        {
            var row = placement.Row;
            while (Overlaps(placement, occupied))
            {
                row++;
                placement.Row = row;
            }

            StampOccupied(placement, occupied);
        }

        return ordered;
    }

    private static bool Overlaps(OccPanelPlacement placement, HashSet<(int Row, int Col)> occupied)
    {
        for (var row = placement.Row; row < placement.Row + placement.RowSpan; row++)
        {
            for (var col = placement.Column; col < placement.Column + placement.ColumnSpan; col++)
            {
                if (occupied.Contains((row, col)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void StampOccupied(OccPanelPlacement placement, HashSet<(int Row, int Col)> occupied)
    {
        for (var row = placement.Row; row < placement.Row + placement.RowSpan; row++)
        {
            for (var col = placement.Column; col < placement.Column + placement.ColumnSpan; col++)
            {
                occupied.Add((row, col));
            }
        }
    }

    private static OccPanelPlacement CreatePlacement(
        string panelId,
        int column,
        int row,
        int columnSpan,
        bool hidden) => new()
    {
        PanelId = panelId,
        Column = column,
        Row = row,
        ColumnSpan = columnSpan,
        RowSpan = 1,
        IsVisible = !hidden,
        MinHeightDp = ResolveMinHeightDp(panelId)
    };

    private static IReadOnlyList<OccPanelPlacement> ReflowSingleColumn(IReadOnlyList<OccPanelPlacement> placements)
    {
        var visible = placements
            .Where(placement => placement.IsVisible)
            .OrderBy(placement =>
                placement.PanelId.Equals(OccLayoutDefaults.KpiStripPanelId, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
            .ThenBy(placement => placement.Row)
            .ThenBy(placement => placement.Column)
            .Select(placement => placement.Clone())
            .ToList();

        for (var row = 0; row < visible.Count; row++)
        {
            visible[row].Column = OccLayoutGridConstants.LeftColumn;
            visible[row].ColumnSpan = OccLayoutGridConstants.FullWidthSpan;
            visible[row].Row = row;
        }

        var visibleIds = visible
            .Select(placement => placement.PanelId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return placements
            .Where(placement => !placement.IsVisible || visibleIds.Contains(placement.PanelId))
            .Select(placement =>
            {
                if (!placement.IsVisible)
                {
                    return placement.Clone();
                }

                return visible.First(candidate =>
                    candidate.PanelId.Equals(placement.PanelId, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
    }

    private static IReadOnlyList<OccPanelPlacement> ReflowScaled(
        IReadOnlyList<OccPanelPlacement> placements,
        double scale)
    {
        var working = placements.Select(placement => placement.Clone()).ToList();
        foreach (var placement in working.Where(candidate => candidate.IsVisible))
        {
            if (placement.PanelId.Equals(OccLayoutDefaults.KpiStripPanelId, StringComparison.OrdinalIgnoreCase))
            {
                placement.Column = OccLayoutGridConstants.LeftColumn;
                placement.ColumnSpan = OccLayoutGridConstants.FullWidthSpan;
                continue;
            }

            var minSpan = ResolveMinColumnSpan(placement.PanelId);
            var scaledSpan = (int)Math.Round(placement.ColumnSpan * scale);
            placement.ColumnSpan = Math.Clamp(
                Math.Max(minSpan, scaledSpan),
                minSpan,
                OccLayoutGridConstants.GridColumns);
            var scaledColumn = (int)Math.Round(placement.Column * scale);
            placement.Column = Math.Clamp(
                scaledColumn,
                0,
                OccLayoutGridConstants.GridColumns - placement.ColumnSpan);
        }

        return ResolveCollisions(working);
    }

    private static IReadOnlyList<string> SanitizeOrder(
        IReadOnlyList<string>? stored,
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
                if (AllowedPanels.Contains(normalized) &&
                    !results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
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
}
