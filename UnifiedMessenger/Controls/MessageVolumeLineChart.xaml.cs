using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;
using Windows.Foundation;

namespace UnifiedMessenger.Controls;

public sealed partial class MessageVolumeLineChart : UserControl
{
    private readonly MessageVolumeLineChartViewModel _viewModel = new();

    public MessageVolumeLineChart()
    {
        InitializeComponent();
    }

    public void ApplySeries(IReadOnlyList<DailyActivityPoint>? series)
    {
        _viewModel.ApplySeries(series);
        SummaryTextBlock.Text = _viewModel.SummaryText;
        EmptyHintText.Visibility = _viewModel.ShowEmptyHint ? Visibility.Visible : Visibility.Collapsed;

        if (_viewModel.ShowEmptyHint || series is null || series.Count == 0)
        {
            LinePath.Data = new PathGeometry();
            AreaPath.Data = new PathGeometry();
            return;
        }

        var chart = MessageVolumeLineChartHelper.Build(series);
        LinePath.Data = BuildLineGeometry(chart.LinePathData, series);
        AreaPath.Data = BuildAreaGeometry(chart.AreaPathData, series);
    }

    private static Geometry BuildLineGeometry(string pathData, IReadOnlyList<DailyActivityPoint> series)
    {
        var points = ExtractPoints(pathData, series);
        if (points.Count == 0)
        {
            return new PathGeometry();
        }

        var figure = new PathFigure { StartPoint = points[0] };
        if (points.Count > 1)
        {
            var segment = new PolyLineSegment();
            for (var i = 1; i < points.Count; i++)
            {
                segment.Points.Add(points[i]);
            }

            figure.Segments.Add(segment);
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Geometry BuildAreaGeometry(string pathData, IReadOnlyList<DailyActivityPoint> series)
    {
        var points = ExtractPoints(pathData, series);
        if (points.Count == 0)
        {
            return new PathGeometry();
        }

        var figure = new PathFigure { StartPoint = points[0] };
        if (points.Count > 1)
        {
            var segment = new PolyLineSegment();
            for (var i = 1; i < points.Count; i++)
            {
                segment.Points.Add(points[i]);
            }

            figure.Segments.Add(segment);
            figure.IsClosed = true;
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static List<Point> ExtractPoints(string pathData, IReadOnlyList<DailyActivityPoint> series)
    {
        _ = pathData;
        var totals = series.Select(point => point.Sent + point.Received).ToList();
        var peak = Math.Max(1, totals.Max());
        const double width = 320;
        const double height = 96;
        var stepX = series.Count <= 1 ? width : width / (series.Count - 1);
        var points = new List<Point>(series.Count);

        for (var i = 0; i < series.Count; i++)
        {
            var x = i * stepX;
            var y = height - (totals[i] * height / peak);
            points.Add(new Point(x, y));
        }

        if (points.Count > 1)
        {
            points.Insert(0, new Point(points[0].X, height));
            points.Add(new Point(points[^1].X, height));
        }

        return points;
    }
}
