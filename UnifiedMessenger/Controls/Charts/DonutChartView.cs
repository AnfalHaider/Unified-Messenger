using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;
using Windows.Foundation;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls.Charts;

/// <summary>
/// A donut chart: a ring of coloured arcs with a centre caption and a legend showing each slice's label
/// and %. Built from <see cref="DonutSlice"/> rows whose percentages already sum to 100
/// (<see cref="ChartSeriesBuilder.BuildShareSlices"/>). Draws nothing but a standard empty state when the
/// slice list is empty, so an all-zero period never shows a full ring of one colour.
/// </summary>
public sealed class DonutChartView : ContentControl
{
    public static readonly DependencyProperty SlicesProperty = DependencyProperty.Register(
        nameof(Slices), typeof(IReadOnlyList<DonutSlice>), typeof(DonutChartView),
        new PropertyMetadata(null, (d, _) => ((DonutChartView)d).Rebuild()));

    public static readonly DependencyProperty CentreCaptionProperty = DependencyProperty.Register(
        nameof(CentreCaption), typeof(string), typeof(DonutChartView),
        new PropertyMetadata(string.Empty, (d, _) => ((DonutChartView)d).Rebuild()));

    public static readonly DependencyProperty EmptyHintProperty = DependencyProperty.Register(
        nameof(EmptyHint), typeof(string), typeof(DonutChartView),
        new PropertyMetadata("No data for this period", (d, _) => ((DonutChartView)d).Rebuild()));

    private const double Diameter = 160;
    private const double Thickness = 22;

    private readonly Canvas _ring = new() { Width = Diameter, Height = Diameter };
    private readonly TextBlock _centre = new()
    {
        FontSize = 12,
        Opacity = 0.7,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.WrapWholeWords,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly StackPanel _legend = new() { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _plot = new();
    private readonly StackPanel _root;

    public DonutChartView()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _plot.Children.Add(_ring);
        _plot.Children.Add(_centre);

        _root = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, VerticalAlignment = VerticalAlignment.Center };
        _root.Children.Add(_plot);
        _root.Children.Add(_legend);
        Content = _root;
        Rebuild();
    }

    public IReadOnlyList<DonutSlice>? Slices
    {
        get => (IReadOnlyList<DonutSlice>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public string CentreCaption
    {
        get => (string)GetValue(CentreCaptionProperty);
        set => SetValue(CentreCaptionProperty, value);
    }

    public string EmptyHint
    {
        get => (string)GetValue(EmptyHintProperty);
        set => SetValue(EmptyHintProperty, value);
    }

    private void Rebuild()
    {
        var slices = Slices;
        if (slices is null || slices.Count == 0)
        {
            Content = new EmptyStateView { IconGlyph = "\uE9D2", Title = EmptyHint, HorizontalAlignment = HorizontalAlignment.Center };
            return;
        }

        Content = _root;
        _ring.Children.Clear();
        _legend.Children.Clear();
        _centre.Text = CentreCaption;

        const double radius = (Diameter - Thickness) / 2;
        var centre = new Point(Diameter / 2, Diameter / 2);
        var startAngle = -90.0; // 12 o'clock

        foreach (var slice in slices)
        {
            var sweep = slice.Percent / 100.0 * 360.0;
            _ring.Children.Add(BuildArc(centre, radius, startAngle, startAngle + sweep, slice.ColorHex));
            startAngle += sweep;
            _legend.Children.Add(BuildLegendRow(slice));
        }

        // Accessibility: the arcs carry no text, so summarise the whole donut for a screen reader.
        var summary = string.Join(", ", slices.Select(s => $"{s.Label} {s.Percent} percent"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, summary);
    }

    private static ShapePath BuildArc(Point centre, double radius, double startDeg, double endDeg, string colorHex)
    {
        var brush = new SolidColorBrush(ParseColor(colorHex));

        // A full 100% slice can't be one arc (start==end); draw it as a ring (two half-circles).
        if (endDeg - startDeg >= 359.999)
        {
            return new ShapePath
            {
                Stroke = brush,
                StrokeThickness = Thickness,
                Data = new EllipseGeometry { Center = centre, RadiusX = radius, RadiusY = radius }
            };
        }

        var start = PointOnCircle(centre, radius, startDeg);
        var end = PointOnCircle(centre, radius, endDeg);
        var large = endDeg - startDeg > 180;

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = large
        });

        return new ShapePath
        {
            Stroke = brush,
            StrokeThickness = Thickness,
            StrokeLineJoin = PenLineJoin.Round,
            Data = new PathGeometry { Figures = { figure } }
        };
    }

    private static Point PointOnCircle(Point centre, double radius, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        return new Point(centre.X + radius * Math.Cos(rad), centre.Y + radius * Math.Sin(rad));
    }

    private FrameworkElement BuildLegendRow(DonutSlice slice)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(ParseColor(slice.ColorHex)), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = slice.Label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = $"{slice.Percent}%", FontSize = 12, FontWeight = FontWeights.SemiBold, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        return row;
    }

    private static Windows.UI.Color ParseColor(string hex) =>
        PlatformBrandingHelper.ParseAccentColor(hex);
}
