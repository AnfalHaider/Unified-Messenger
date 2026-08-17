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

    /// <summary>
    /// Whether the tile is a tab stop. A tile with no drill-down is still rendered as a Button (so every
    /// tile keeps one visual treatment) but must not collect a tab stop it does nothing with — that would
    /// add six dead stops to the keyboard path for the sake of visual consistency.
    /// </summary>
    public bool IsActionable => HasAction;

    /// <summary>
    /// Whether this tile carries the screen's primary answer. Exactly one tile may claim it.
    /// </summary>
    /// <remarks>
    /// Six tiles at identical size and weight is the same as no hierarchy: "Busiest window · 7PM" was
    /// drawn exactly as loudly as the number of customers waiting. Supporting metrics are now rendered a
    /// step quieter so the eye has somewhere to land.
    /// </remarks>
    public bool IsPrimary { get; init; }

    /// <summary>Value type size — the one lever that gives the band a hierarchy.</summary>
    public double ValueFontSize => ValueFontSizeFor(IsPrimary);

    /// <summary>
    /// The type step between the primary tile and the rest. Static so the gap can be asserted without a
    /// XAML runtime — constructing this view model needs a <see cref="Brush"/>, which does not exist in a
    /// headless test host.
    /// </summary>
    public static double ValueFontSizeFor(bool isPrimary) => isPrimary ? 26 : 20;

    /// <summary>
    /// What a screen reader says for the whole tile, in one breath: what it measures, the figure, the
    /// qualifier, and whether pressing it does anything.
    ///
    /// <para>
    /// Needed because the tile used to be a <c>Border</c>, which derives no name at all — a UI Automation
    /// capture of the running app showed the tiles as three unrelated Text nodes with no indication they
    /// were interactive.
    /// </para>
    /// </summary>
    public string AccessibleName => ComposeAccessibleName(Label, Value, Delta, Hint, HasAction);

    /// <summary>
    /// Pure composition, kept static so it is testable without a XAML runtime. Order matters: what it
    /// measures, the figure, which way it moved, the qualifier, then whether pressing it does anything.
    /// </summary>
    public static string ComposeAccessibleName(
        string label,
        string value,
        string delta,
        string hint,
        bool hasAction)
    {
        var text = $"{label}: {value}";

        if (!string.IsNullOrEmpty(delta))
        {
            // "▼ 56%" read aloud as a glyph name tells the listener nothing about direction.
            var direction = delta.StartsWith('▲') ? "up" : delta.StartsWith('▼') ? "down" : string.Empty;
            var magnitude = delta.TrimStart('▲', '▼', ' ');
            text += direction.Length > 0 ? $", {direction} {magnitude}" : $", {magnitude}";
        }

        if (!string.IsNullOrWhiteSpace(hint))
        {
            text += $". {hint}";
        }

        return hasAction ? $"{text}. Press to see details." : text;
    }
}
