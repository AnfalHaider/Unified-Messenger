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
    private readonly PathGeometry _lineGeometry = new();
    private readonly PathGeometry _areaGeometry = new();
    private string? _lastSeriesSignature;

    public MessageVolumeLineChart()
    {
        InitializeComponent();
        LinePath.Data = _lineGeometry;
        AreaPath.Data = _areaGeometry;
    }

    public void ApplySeries(IReadOnlyList<DailyActivityPoint>? series)
    {
        _viewModel.ApplySeries(series);
        SummaryTextBlock.Text = _viewModel.SummaryText;
        EmptyHintText.Visibility = _viewModel.ShowEmptyHint ? Visibility.Visible : Visibility.Collapsed;

        if (_viewModel.ShowEmptyHint || series is null || series.Count == 0)
        {
            _lastSeriesSignature = null;
            _lineGeometry.Figures.Clear();
            _areaGeometry.Figures.Clear();
            return;
        }

        var signature = BuildSeriesSignature(series);
        if (signature == _lastSeriesSignature)
        {
            return;
        }

        _lastSeriesSignature = signature;
        UpdateLineGeometry(_lineGeometry, series, closeArea: false);
        UpdateLineGeometry(_areaGeometry, series, closeArea: true);
    }

    private static string BuildSeriesSignature(IReadOnlyList<DailyActivityPoint> series)
    {
        var builder = new System.Text.StringBuilder(series.Count * 12);
        foreach (var point in series)
        {
            builder.Append(point.Label)
                .Append(':')
                .Append(point.Sent)
                .Append('/')
                .Append(point.Received)
                .Append('|');
        }

        return builder.ToString();
    }

    private static void UpdateLineGeometry(
        PathGeometry geometry,
        IReadOnlyList<DailyActivityPoint> series,
        bool closeArea)
    {
        var points = BuildChartPoints(series);
        geometry.Figures.Clear();

        if (points.Count == 0)
        {
            return;
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
            figure.IsClosed = closeArea;
        }

        geometry.Figures.Add(figure);
    }

    private static List<Point> BuildChartPoints(IReadOnlyList<DailyActivityPoint> series)
    {
        var totals = series.Select(point => point.Sent + point.Received).ToList();
        var peak = Math.Max(1, totals.Max());
        const double width = 320;
        const double height = 96;
        var stepX = series.Count <= 1 ? width : width / (series.Count - 1);
        var points = new List<Point>(series.Count + 2);

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
