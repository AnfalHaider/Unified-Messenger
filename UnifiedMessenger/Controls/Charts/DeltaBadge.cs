using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls.Charts;

/// <summary>
/// The "▲ 12% vs last week" chip. Colour follows the delta's <see cref="DeltaSentiment"/> — good, bad, or
/// no-judgement — not its arrow direction, so response time falling shows a green down-arrow while raw
/// message volume falling shows a neutral one. Feed it a <see cref="MetricDelta"/> from
/// <see cref="ChartSeriesBuilder.ComputeDelta"/>; it hides itself when there is no prior period to compare.
/// <para>
/// Sentiment is carried three ways — colour, a tick/warning glyph, and the words "better"/"worse" in the
/// automation name — because it used to be carried by colour alone. The arrow only ever encoded
/// direction, so "20 percent down" was all a screen reader said for both good and bad news.
/// </para>
/// </summary>
public sealed class DeltaBadge : ContentControl
{
    public static readonly DependencyProperty DeltaProperty = DependencyProperty.Register(
        nameof(Delta), typeof(MetricDelta), typeof(DeltaBadge),
        new PropertyMetadata(MetricDelta.None, (d, _) => ((DeltaBadge)d).Rebuild()));

    public static readonly DependencyProperty ComparisonLabelProperty = DependencyProperty.Register(
        nameof(ComparisonLabel), typeof(string), typeof(DeltaBadge),
        new PropertyMetadata("vs last week", (d, _) => ((DeltaBadge)d).Rebuild()));

    private readonly FontIcon _arrow = new() { FontSize = UmScale.Icon.Sm, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _percent = new() { FontSize = UmScale.Icon.Sm, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _comparison = new() { FontSize = UmScale.Icon.Sm, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };

    /// <summary>Non-colour cue for whether the change is good or bad. See <see cref="Rebuild"/>.</summary>
    private readonly FontIcon _meaning = new() { FontSize = UmScale.Icon.Sm, VerticalAlignment = VerticalAlignment.Center };

    public DeltaBadge()
    {
        IsTabStop = false;
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(_arrow);
        stack.Children.Add(_percent);
        stack.Children.Add(_meaning);
        stack.Children.Add(_comparison);
        Content = stack;
        Rebuild();
    }

    public MetricDelta Delta
    {
        get => (MetricDelta)GetValue(DeltaProperty);
        set => SetValue(DeltaProperty, value);
    }

    public string ComparisonLabel
    {
        get => (string)GetValue(ComparisonLabelProperty);
        set => SetValue(ComparisonLabelProperty, value);
    }

    private void Rebuild()
    {
        var delta = Delta;
        if (!delta.HasData || delta.Direction == DeltaDirection.None)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        _arrow.Glyph = delta.Direction == DeltaDirection.Up ? "\uE70E" : "\uE70D"; // chevron up / down
        _percent.Text = $"{delta.Percent}%";
        _comparison.Text = ComparisonLabel;

        var brushKey = delta.Sentiment switch
        {
            DeltaSentiment.Favourable => "UmStatusSuccessBrush",
            DeltaSentiment.Adverse => "UmStatusDangerBrush",
            _ => "UmStatusNeutralBrush"
        };
        var brush = UmSemanticBrushes.Get(brushKey);
        _arrow.Foreground = brush;
        _percent.Foreground = brush;

        // Whether a change is GOOD was carried by the brush and nothing else: a 20% drop in response time
        // is good news and a 20% drop in messages is bad, and both rendered as "20 percent down". Anyone
        // using Narrator, or with red/green colour-vision deficiency, got the direction and not the
        // meaning. WCAG 1.4.1 — never colour alone — and this control's own class doc said the colour was
        // the only carrier.
        var dir = delta.Direction == DeltaDirection.Up ? "up" : "down";
        var meaning = delta.Sentiment switch
        {
            DeltaSentiment.Favourable => ", better",
            DeltaSentiment.Adverse => ", worse",
            _ => string.Empty
        };

        _meaning.Glyph = delta.Sentiment switch
        {
            DeltaSentiment.Favourable => "", // CheckMark
            DeltaSentiment.Adverse => "",    // Warning
            _ => string.Empty
        };
        _meaning.Foreground = brush;
        _meaning.Visibility = _meaning.Glyph.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        AutomationProperties.SetName(this, $"{delta.Percent} percent {dir}{meaning} {ComparisonLabel}");
    }
}
