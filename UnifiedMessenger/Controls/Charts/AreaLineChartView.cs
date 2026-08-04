using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Services;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;

namespace UnifiedMessenger.Controls.Charts;

/// <summary>
/// A smooth-ish line chart with a gradient fill under it (the mockup's "Average Response Time" /
/// "Replies within 15 minutes" charts). Generic over a plain value series so it can chart minutes,
/// percentages or counts; the caller supplies the value formatter for the y-axis and the accent colour.
/// Resizes with its host and shows a standard empty state when there is no data.
/// </summary>
public sealed class AreaLineChartView : ContentControl
{
    private readonly Canvas _canvas = new();
    private readonly TextBlock _yMax = new() { FontSize = 10, Opacity = 0.6 };
    private readonly TextBlock _xFirst = new() { FontSize = 10, Opacity = 0.6 };
    private readonly TextBlock _xLast = new() { FontSize = 10, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Grid _root;

    private IReadOnlyList<double> _values = [];
    private IReadOnlyList<string> _labels = [];
    private string _colorHex = "#1B75BB";
    private Func<double, string> _formatY = v => ChartSeriesBuilder.FormatAxisCount(v);
    private string _emptyHint = "No data for this period";

    private readonly EmptyStateView _empty = new()
    {
        IconGlyph = "\uE9D2",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };

    public AreaLineChartView()
    {
        IsTabStop = false;
        MinHeight = 120;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        var plot = new Grid { MinHeight = 96 };
        plot.Children.Add(_canvas);
        plot.Children.Add(_yMax); // top-left y-max label overlaid
        plot.Children.Add(_empty); // overlay, not a Content swap — see Redraw

        var axis = new Grid();
        axis.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        axis.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_xFirst, 0);
        Grid.SetColumn(_xLast, 1);
        axis.Children.Add(_xFirst);
        axis.Children.Add(_xLast);

        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(plot, 0);
        Grid.SetRow(axis, 1);
        _root.Children.Add(plot);
        _root.Children.Add(axis);

        Content = _root;
        _canvas.SizeChanged += (_, _) => Redraw();
    }

    /// <summary>Sets the series. <paramref name="formatY"/> renders the y-max label (minutes, %, count).</summary>
    public void SetSeries(
        IReadOnlyList<double> values,
        IReadOnlyList<string> labels,
        string colorHex,
        Func<double, string>? formatY = null,
        string? emptyHint = null)
    {
        _values = values ?? [];
        _labels = labels ?? [];
        _colorHex = string.IsNullOrWhiteSpace(colorHex) ? "#1B75BB" : colorHex;
        _formatY = formatY ?? (v => ChartSeriesBuilder.FormatAxisCount(v));
        if (!string.IsNullOrWhiteSpace(emptyHint))
        {
            _emptyHint = emptyHint!;
        }

        Redraw();
    }

    private void Redraw()
    {
        // The visual tree is kept STABLE and the empty state is an overlay rather than a Content swap.
        // Swapping Content re-parents the Canvas, and because its size doesn't actually change on the way
        // back in, SizeChanged never fires again \u2014 ActualWidth stays 0 and the chart silently never draws.
        _canvas.Children.Clear();

        var hasData = _values.Count > 0 && _values.Any(v => v > 0);
        _empty.Title = _emptyHint;
        _empty.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
        _yMax.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        if (!hasData)
        {
            _xFirst.Text = string.Empty;
            _xLast.Text = string.Empty;
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        if (width <= 1 || height <= 1)
        {
            return; // not measured yet; SizeChanged calls back once layout lands
        }

        var max = Math.Max(1.0, _values.Max());
        var stepX = _values.Count <= 1 ? width : width / (_values.Count - 1);
        var points = new List<Point>(_values.Count);
        for (var i = 0; i < _values.Count; i++)
        {
            var x = i * stepX;
            var y = height - _values[i] / max * height;
            points.Add(new Point(x, y));
        }

        var accent = PlatformBrandingHelper.ParseAccentColor(_colorHex);

        // Gradient fill under the line, fading to transparent at the baseline.
        var area = new PathFigure { StartPoint = new Point(points[0].X, height), IsClosed = true };
        area.Segments.Add(new LineSegment { Point = points[0] });
        foreach (var p in points.Skip(1))
        {
            area.Segments.Add(new LineSegment { Point = p });
        }

        area.Segments.Add(new LineSegment { Point = new Point(points[^1].X, height) });
        _canvas.Children.Add(new ShapePath
        {
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(120, accent.R, accent.G, accent.B), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0, accent.R, accent.G, accent.B), Offset = 1 }
                }
            },
            Data = new PathGeometry { Figures = { area } }
        });

        // The line itself.
        var line = new PathFigure { StartPoint = points[0], IsClosed = false };
        foreach (var p in points.Skip(1))
        {
            line.Segments.Add(new LineSegment { Point = p });
        }

        _canvas.Children.Add(new ShapePath
        {
            Stroke = new SolidColorBrush(accent),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            Data = new PathGeometry { Figures = { line } }
        });

        _yMax.Text = _formatY(max);
        _xFirst.Text = _labels.Count > 0 ? _labels[0] : string.Empty;
        _xLast.Text = _labels.Count > 0 ? _labels[^1] : string.Empty;
    }
}
