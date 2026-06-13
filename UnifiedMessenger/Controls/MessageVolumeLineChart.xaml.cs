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
    private const double DefaultPlotWidth = 320;

    private readonly MessageVolumeLineChartViewModel _viewModel = new();
    private IReadOnlyList<DailyActivityPoint>? _currentSeries;
    private string? _lastRenderedSignature;
    private bool _rangeExceedsDisplayCap;

    public MessageVolumeLineChart()
    {
        InitializeComponent();
        ChartPlotGrid.SizeChanged += OnChartPlotSizeChanged;
        Loaded += (_, _) => RenderChart();
    }

    public void ApplySeries(IReadOnlyList<DailyActivityPoint>? series, bool rangeExceedsDisplayCap = false)
    {
        _currentSeries = series;
        _rangeExceedsDisplayCap = rangeExceedsDisplayCap;
        _viewModel.ApplySeries(
            series,
            ResolvePlotWidth(),
            ResolvePlotHeight(),
            rangeExceedsDisplayCap,
            IsHistoricalMode);
        SummaryTextBlock.Text = _viewModel.SummaryText;
        EmptyHintText.Visibility = _viewModel.ShowEmptyHint ? Visibility.Visible : Visibility.Collapsed;
        if (_viewModel.ShowEmptyHint)
        {
            EmptyHintText.Text = IsHistoricalMode
                ? "Historical volume appears after message history is synced for the selected period."
                : "Volume appears after WhatsApp message history is synced. KPI cards show your live queue.";
        }

        UpdateAxisLabels(series);
        RenderChart();
    }

    public bool IsHistoricalMode { get; set; }

    private void UpdateAxisLabels(IReadOnlyList<DailyActivityPoint>? series)
    {
        if (series is null || series.Count == 0)
        {
            AxisStartLabel.Text = "—";
            AxisEndLabel.Text = "—";
            return;
        }

        AxisStartLabel.Text = series[0].Label;
        AxisEndLabel.Text = series[^1].Label;
    }

    private void OnChartPlotSizeChanged(object sender, SizeChangedEventArgs e) =>
        RenderChart();

    private void RenderChart()
    {
        if (_viewModel.ShowEmptyHint || _currentSeries is null || _currentSeries.Count == 0)
        {
            _lastRenderedSignature = null;
            LinePath.Data = null;
            AreaPath.Data = null;
            return;
        }

        var width = ResolvePlotWidth();
        var height = ResolvePlotHeight();
        var chart = MessageVolumeLineChartHelper.Build(_currentSeries, width, height, _rangeExceedsDisplayCap);
        var signature = $"{chart.LinePathData}|{chart.AreaPathData}|{width:0.#}|{height:0.#}";
        if (signature == _lastRenderedSignature)
        {
            return;
        }

        _lastRenderedSignature = signature;
        LinePath.Data = CreateGeometry(chart.LinePathData);
        AreaPath.Data = CreateGeometry(chart.AreaPathData);
    }

    private double ResolvePlotWidth()
    {
        var width = ChartPlotGrid.ActualWidth;
        return width > 0 ? width : DefaultPlotWidth;
    }

    private static double ResolvePlotHeight() =>
        Application.Current.Resources.TryGetValue("UmChartPlotHeight", out var token) &&
        token is double plotHeight
            ? plotHeight
            : 96;

    private static Geometry? CreateGeometry(string pathData)
    {
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return null;
        }

        var geometry = new PathGeometry();
        var tokens = pathData.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        PathFigure? figure = null;
        PolyLineSegment? segment = null;

        for (var i = 0; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "M":
                    if (figure is not null)
                    {
                        if (segment?.Points.Count > 0)
                        {
                            figure.Segments.Add(segment);
                        }

                        geometry.Figures.Add(figure);
                    }

                    figure = new PathFigure
                    {
                        StartPoint = ParsePoint(tokens[++i])
                    };
                    segment = new PolyLineSegment();
                    break;
                case "L":
                    segment?.Points.Add(ParsePoint(tokens[++i]));
                    break;
                case "Z":
                    if (figure is not null)
                    {
                        figure.IsClosed = true;
                    }

                    break;
            }
        }

        if (figure is not null)
        {
            if (segment?.Points.Count > 0)
            {
                figure.Segments.Add(segment);
            }

            geometry.Figures.Add(figure);
        }

        return geometry;
    }

    private static Point ParsePoint(string token)
    {
        var parts = token.Split(',');
        return new Point(double.Parse(parts[0]), double.Parse(parts[1]));
    }
}
