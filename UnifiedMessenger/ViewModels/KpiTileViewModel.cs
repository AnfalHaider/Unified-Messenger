using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UnifiedMessenger.ViewModels;

/// <summary>
/// One tile in the command-center KPI band. Data-driven so the band can render through an
/// <c>ItemsRepeater</c> + <c>UniformGridLayout</c> (it reflows/wraps responsively at any width) instead of a
/// fixed-column grid. <see cref="ActionKey"/> routes a tap to the matching drill-down.
/// </summary>
public sealed class KpiTileViewModel
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public string Hint { get; init; } = string.Empty;

    /// <summary>Optional ▲/▼ delta shown next to the value (empty = none).</summary>
    public string Delta { get; init; } = string.Empty;

    public required Brush ValueBrush { get; init; }

    public Brush? DeltaBrush { get; init; }

    /// <summary>Routing key for a tap (e.g. "awaiting", "busiest", "caughtup"). Empty = not clickable.</summary>
    public string ActionKey { get; init; } = string.Empty;

    public string Tooltip { get; init; } = string.Empty;

    /// <summary>Optional recent daily trend for a mini-sparkline (null/short = hidden).</summary>
    public IReadOnlyList<int>? Trend { get; init; }

    public bool HasAction => !string.IsNullOrEmpty(ActionKey);

    public bool HasDelta => !string.IsNullOrEmpty(Delta);

    public Visibility DeltaVisibility => HasDelta ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Hover/pressed affordance only appears on interactive tiles.</summary>
    public bool IsInteractive => HasAction;
}
