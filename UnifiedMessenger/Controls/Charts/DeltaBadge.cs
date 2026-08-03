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
/// </summary>
public sealed class DeltaBadge : ContentControl
{
    public static readonly DependencyProperty DeltaProperty = DependencyProperty.Register(
        nameof(Delta), typeof(MetricDelta), typeof(DeltaBadge),
        new PropertyMetadata(MetricDelta.None, (d, _) => ((DeltaBadge)d).Rebuild()));

    public static readonly DependencyProperty ComparisonLabelProperty = DependencyProperty.Register(
        nameof(ComparisonLabel), typeof(string), typeof(DeltaBadge),
        new PropertyMetadata("vs last week", (d, _) => ((DeltaBadge)d).Rebuild()));

    private readonly FontIcon _arrow = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _percent = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _comparison = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };

    public DeltaBadge()
    {
        IsTabStop = false;
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(_arrow);
        stack.Children.Add(_percent);
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

        var dir = delta.Direction == DeltaDirection.Up ? "up" : "down";
        AutomationProperties.SetName(this, $"{delta.Percent} percent {dir} {ComparisonLabel}");
    }
}
