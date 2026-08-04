using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls.Charts;

/// <summary>
/// A labeled bar chart (the mockup's "Messages Over Time"). Star-column layout, max-scaled with a
/// min-height floor so a tiny non-zero bar is still visible, the peak bar highlighted with a value label,
/// x-axis labels thinned when crowded, and a per-bar tooltip. Single-series; the stacked-by-account
/// renderer stays in <c>ActivityPatternsPanel</c> until that panel migrates onto this control.
/// </summary>
public sealed class BarChartView : ContentControl
{
    private const double BarMaxHeight = 165;

    private readonly Grid _chart = new();
    private readonly Grid _axis = new();
    private readonly Grid _root;

    private IReadOnlyList<(string Label, double Value)> _bars = [];
    private string _colorHex = "#1B75BB";
    private string _emptyHint = "No data for this period";

    private readonly EmptyStateView _empty = new()
    {
        IconGlyph = "\uE9D2",
        HorizontalAlignment = HorizontalAlignment.Center,
        Visibility = Visibility.Collapsed
    };

    public BarChartView()
    {
        IsTabStop = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _chart.Height = BarMaxHeight + 22; // room for the peak value label above the tallest bar
        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_chart, 0);
        Grid.SetRow(_axis, 1);
        _root.Children.Add(_chart);
        _root.Children.Add(_axis);
        _root.Children.Add(_empty); // overlay; never swap Content (see AreaLineChartView.Redraw)
        Grid.SetRowSpan(_empty, 2);
        Content = _root;
    }

    public void SetBars(
        IReadOnlyList<(string Label, double Value)> bars,
        string colorHex,
        string? emptyHint = null)
    {
        _bars = bars ?? [];
        _colorHex = string.IsNullOrWhiteSpace(colorHex) ? "#1B75BB" : colorHex;
        if (!string.IsNullOrWhiteSpace(emptyHint))
        {
            _emptyHint = emptyHint!;
        }

        Redraw();
    }

    private void Redraw()
    {
        var hasData = _bars.Count > 0 && _bars.Any(b => b.Value > 0);
        _empty.Title = _emptyHint;
        _empty.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
        _chart.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        _axis.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        if (!hasData)
        {
            return;
        }

        _chart.Children.Clear();
        _chart.ColumnDefinitions.Clear();
        _axis.Children.Clear();
        _axis.ColumnDefinitions.Clear();

        var max = Math.Max(1.0, _bars.Max(b => b.Value));
        var peakIndex = 0;
        for (var i = 1; i < _bars.Count; i++)
        {
            if (_bars[i].Value > _bars[peakIndex].Value)
            {
                peakIndex = i;
            }
        }

        var accent = new SolidColorBrush(PlatformBrandingHelper.ParseAccentColor(_colorHex));
        var caution = UmSemanticBrushes.Get("UmStatusWarningBrush");

        for (var i = 0; i < _bars.Count; i++)
        {
            _chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _axis.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var (label, value) = _bars[i];
            var isPeak = i == peakIndex;

            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(3, 0, 3, 0) };

            if (isPeak)
            {
                column.Children.Add(new TextBlock
                {
                    Text = ChartSeriesBuilder.FormatAxisCount(value),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = caution,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            var barHeight = Math.Max(2, value / max * BarMaxHeight);
            column.Children.Add(new Border
            {
                Height = barHeight,
                Background = isPeak ? caution : accent,
                Opacity = isPeak ? 1.0 : 0.55,
                CornerRadius = new CornerRadius(3, 3, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            });

            ToolTipService.SetToolTip(column, $"{label} · {ChartSeriesBuilder.FormatAxisCount(value)}");
            Grid.SetColumn(column, i);
            _chart.Children.Add(column);

            // Axis label thinning: all when few, else every 3rd + always the peak.
            var showLabel = _bars.Count <= 12 || i % 3 == 0 || isPeak;
            if (showLabel)
            {
                var axisLabel = new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    Foreground = isPeak ? caution : UmSemanticBrushes.Get("UmStatusNeutralBrush"),
                    FontWeight = isPeak ? FontWeights.SemiBold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(axisLabel, i);
                _axis.Children.Add(axisLabel);
            }
        }

        var total = _bars.Sum(b => b.Value);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            this, $"Bar chart, {_bars.Count} bars, total {ChartSeriesBuilder.FormatAxisCount(total)}, peak {_bars[peakIndex].Label}");
    }
}
