using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls.Occ;

/// <summary>
/// Applies resolved grid placements to a 12-column WinUI grid.
/// </summary>
public static class OccLayoutGridApplier
{
    public static void Apply(
        Grid grid,
        IReadOnlyList<OccPanelPlacement> placements,
        IReadOnlyDictionary<string, FrameworkElement> panelMap)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(panelMap);

        EnsureColumnDefinitions(grid);
        EnsureRowDefinitions(grid, placements);

        foreach (var (panelId, element) in panelMap)
        {
            var placement = placements.FirstOrDefault(candidate =>
                candidate.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));

            if (placement is null)
            {
                element.Visibility = Visibility.Collapsed;
                continue;
            }

            if (!placement.IsVisible)
            {
                element.Visibility = Visibility.Collapsed;
                continue;
            }

            element.Visibility = Visibility.Visible;
            if (!grid.Children.Contains(element))
            {
                grid.Children.Add(element);
            }

            Grid.SetColumn(element, placement.Column);
            Grid.SetRow(element, placement.Row);
            Grid.SetColumnSpan(element, placement.ColumnSpan);
            Grid.SetRowSpan(element, placement.RowSpan);

            if (placement.MinHeightDp > 0)
            {
                element.MinHeight = placement.MinHeightDp;
            }
        }
    }

    private static void EnsureColumnDefinitions(Grid grid)
    {
        while (grid.ColumnDefinitions.Count < OccLayoutGridConstants.GridColumns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        while (grid.ColumnDefinitions.Count > OccLayoutGridConstants.GridColumns)
        {
            grid.ColumnDefinitions.RemoveAt(grid.ColumnDefinitions.Count - 1);
        }
    }

    private static void EnsureRowDefinitions(Grid grid, IReadOnlyList<OccPanelPlacement> placements)
    {
        var requiredRows = placements.Count == 0
            ? 1
            : placements.Max(placement => placement.Row + placement.RowSpan);

        while (grid.RowDefinitions.Count < requiredRows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        while (grid.RowDefinitions.Count > requiredRows)
        {
            grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);
        }
    }
}
