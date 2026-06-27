using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

/// <summary>
/// One filterable activity-patterns graph: inbound (customer) message volume bucketed by hour of day,
/// day of week, or month, across all professional accounts or one, over a chosen date range. Reads the
/// on-device activity history persisted by <see cref="MessageAnalyticsService"/> — fully local, no cloud.
/// </summary>
public sealed partial class ActivityPatternsPanel : UserControl
{
    private const double ChartHeight = 190;

    private ApplicationServices? _services;
    private ActivityDimension _dimension = ActivityDimension.HourOfDay;
    private bool _populatingAccounts;
    private double _lastChartWidth;

    public ActivityPatternsPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void ConfigureServices(ApplicationServices services) => _services = services;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_services is null && ApplicationServiceProvider.IsInitialized)
        {
            _services = ApplicationServiceProvider.Current;
        }

        PopulateAccounts();
        Render();

        // Re-draw on width change so the curve + axis labels reflow with the window.
        ChartHost.SizeChanged += (_, args) =>
        {
            if (Math.Abs(args.NewSize.Width - _lastChartWidth) > 1)
            {
                Render();
            }
        };
    }

    private void PopulateAccounts()
    {
        if (_services is null)
        {
            return;
        }

        _populatingAccounts = true;
        AccountSelector.Items.Clear();
        AccountSelector.Items.Add(new ComboBoxItem { Content = "All accounts", Tag = string.Empty });
        foreach (var instance in ProfessionalInstances())
        {
            AccountSelector.Items.Add(new ComboBoxItem { Content = instance.DisplayName, Tag = instance.Id });
        }

        AccountSelector.SelectedIndex = 0;
        _populatingAccounts = false;
    }

    private IEnumerable<MessengerInstance> ProfessionalInstances() =>
        _services?.Registry.Instances
            .Where(i => i.IsProfessional && PlatformModuleSettingsHelper.IsPlatformModuleEnabled(i.Platform))
        ?? [];

    private void OnHourClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.HourOfDay);

    private void OnDayClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.DayOfWeek);

    private void OnMonthClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.Month);

    private void SelectDimension(ActivityDimension dimension)
    {
        _dimension = dimension;
        HourButton.IsChecked = dimension == ActivityDimension.HourOfDay;
        DayButton.IsChecked = dimension == ActivityDimension.DayOfWeek;
        MonthButton.IsChecked = dimension == ActivityDimension.Month;
        Render();
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_populatingAccounts)
        {
            Render();
        }
    }

    private IReadOnlyList<MessengerInstance> ResolveInstances()
    {
        var selectedId = (AccountSelector.SelectedItem as ComboBoxItem)?.Tag as string;
        if (!string.IsNullOrEmpty(selectedId))
        {
            return ProfessionalInstances().Where(i => i.Id == selectedId).ToList();
        }

        return ProfessionalInstances().ToList();
    }

    private (DateTimeOffset? From, DateTimeOffset? To) ResolveRange()
    {
        var now = DateTimeOffset.Now;
        return ((RangeSelector.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "30" => (now.AddDays(-30), (DateTimeOffset?)null),
            "year" => (new DateTimeOffset(new DateTime(now.Year, 1, 1), now.Offset), null),
            "all" => (null, null),
            _ => (now.AddDays(-90), null)
        };
    }

    /// <summary>Re-queries the history log for the current filters and redraws the bars + insight.</summary>
    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var instances = ResolveInstances();
        var (from, to) = ResolveRange();
        var patterns = MessageAnalyticsService.Instance.BuildActivityPatterns(_dimension, instances, from, to);

        ChartHost.Children.Clear();
        AxisHost.Children.Clear();

        var count = patterns.Labels.Count;
        if (count == 0 || !patterns.HasData)
        {
            ChartHost.Children.Add(new TextBlock
            {
                Text = "No activity recorded yet for this selection — patterns build up as messages arrive.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords
            });
            InsightText.Text = "Once a few days of activity accrue, the busiest hour, day, and month surface here.";
            WeekRow.Visibility = Visibility.Collapsed;
            return;
        }

        DrawAreaChart(patterns);

        var prep = _dimension switch
        {
            ActivityDimension.HourOfDay => "around",
            ActivityDimension.DayOfWeek => "on",
            _ => "in"
        };
        InsightText.Text = $"Busiest {prep} {patterns.PeakLabel} — {patterns.Values[patterns.PeakIndex]} messages at the peak.";

        RenderWeekOverWeek(instances);
    }

    /// <summary>
    /// Draws the activity volume as a smooth gradient area + line with point markers and a highlighted
    /// peak — replacing the old bar columns. Axis labels are positioned on a Canvas so they sit directly
    /// under each data point regardless of bucket count.
    /// </summary>
    private void DrawAreaChart(ActivityPatterns patterns)
    {
        var count = patterns.Values.Count;
        var width = ChartHost.ActualWidth;
        if (width < 40)
        {
            width = 560; // first layout pass before the host has measured; SizeChanged re-draws to fit.
        }
        _lastChartWidth = width;

        var canvas = new Canvas { Width = width, Height = ChartHeight };
        ChartHost.Children.Add(canvas);

        const double topPad = 24;    // headroom for the peak value label
        const double bottomPad = 8;
        const double sidePad = 12;
        var plotTop = topPad;
        var plotBottom = ChartHeight - bottomPad;
        var plotHeight = plotBottom - plotTop;
        var plotLeft = sidePad;
        var plotWidth = Math.Max(1, width - sidePad * 2);

        var max = Math.Max(1, patterns.Values.Max());
        var accent = Brush("AccentFillColorDefaultBrush");
        var accentColor = (accent as SolidColorBrush)?.Color ?? Colors.SteelBlue;
        var peakFill = Brush("SystemFillColorCautionBrush");
        var peakColor = (peakFill as SolidColorBrush)?.Color ?? Colors.Goldenrod;
        var cardBg = Brush("CardBackgroundFillColorDefaultBrush");
        var gridBrush = Brush("DividerStrokeColorDefaultBrush");
        var labelBrush = Brush("TextFillColorTertiaryBrush");

        // Faint horizontal gridlines.
        for (var g = 0; g <= 3; g++)
        {
            var y = plotTop + plotHeight * g / 3.0;
            canvas.Children.Add(new Line
            {
                X1 = plotLeft,
                X2 = plotLeft + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                Opacity = 0.35
            });
        }

        // Data points in plot space.
        var pts = new List<Point>(count);
        for (var i = 0; i < count; i++)
        {
            var x = count == 1 ? plotLeft + plotWidth / 2 : plotLeft + plotWidth * i / (count - 1.0);
            var y = plotBottom - patterns.Values[i] / (double)max * plotHeight;
            pts.Add(new Point(x, y));
        }

        // Gradient area fill under the curve.
        var areaGeo = new PathGeometry();
        areaGeo.Figures.Add(BuildSmoothFigure(pts, closeArea: true, baselineY: plotBottom));
        var areaGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        areaGradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(96, accentColor.R, accentColor.G, accentColor.B), Offset = 0 });
        areaGradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(6, accentColor.R, accentColor.G, accentColor.B), Offset = 1 });
        canvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Path { Data = areaGeo, Fill = areaGradient });

        // The line itself.
        var lineGeo = new PathGeometry();
        lineGeo.Figures.Add(BuildSmoothFigure(pts, closeArea: false, baselineY: plotBottom));
        canvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = lineGeo,
            Stroke = accent,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });

        // Point markers (peak emphasized) + tooltips.
        for (var i = 0; i < count; i++)
        {
            var isPeak = i == patterns.PeakIndex;
            var p = pts[i];
            var size = isPeak ? 11.0 : 6.0;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = isPeak ? peakFill : accent,
                Stroke = cardBg,
                StrokeThickness = 2
            };
            Canvas.SetLeft(dot, p.X - size / 2);
            Canvas.SetTop(dot, p.Y - size / 2);
            ToolTipService.SetToolTip(dot, $"{patterns.Labels[i]} · {patterns.Values[i]} messages");
            canvas.Children.Add(dot);

            if (isPeak)
            {
                var lbl = new TextBlock
                {
                    Text = patterns.Values[i].ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = peakFill
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var lx = Math.Clamp(p.X - lbl.DesiredSize.Width / 2, 0, width - lbl.DesiredSize.Width);
                Canvas.SetLeft(lbl, lx);
                Canvas.SetTop(lbl, Math.Max(0, p.Y - size / 2 - lbl.DesiredSize.Height - 2));
                canvas.Children.Add(lbl);
            }
        }

        // Axis labels aligned under each point. Thin to every 3rd for the dense 24-hour view.
        for (var i = 0; i < count; i++)
        {
            var isPeak = i == patterns.PeakIndex;
            var showLabel = count <= 12 || i % 3 == 0 || isPeak;
            if (!showLabel)
            {
                continue;
            }

            var axisLabel = new TextBlock
            {
                Text = patterns.Labels[i],
                FontSize = 10,
                Foreground = isPeak ? peakFill : labelBrush,
                FontWeight = isPeak ? FontWeights.SemiBold : FontWeights.Normal
            };
            axisLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var ax = Math.Clamp(pts[i].X - axisLabel.DesiredSize.Width / 2, 0, width - axisLabel.DesiredSize.Width);
            Canvas.SetLeft(axisLabel, ax);
            Canvas.SetTop(axisLabel, 0);
            AxisHost.Children.Add(axisLabel);
        }
    }

    /// <summary>
    /// Builds a Catmull-Rom-smoothed path figure through <paramref name="pts"/>. When
    /// <paramref name="closeArea"/> is set, the figure drops to <paramref name="baselineY"/> at both ends
    /// and closes, yielding a fillable area under the curve.
    /// </summary>
    private static PathFigure BuildSmoothFigure(IReadOnlyList<Point> pts, bool closeArea, double baselineY)
    {
        var fig = new PathFigure();
        if (closeArea)
        {
            fig.StartPoint = new Point(pts[0].X, baselineY);
            fig.Segments.Add(new LineSegment { Point = pts[0] });
        }
        else
        {
            fig.StartPoint = pts[0];
        }

        if (pts.Count == 1)
        {
            if (closeArea)
            {
                fig.Segments.Add(new LineSegment { Point = new Point(pts[0].X, baselineY) });
                fig.IsClosed = true;
            }
            return fig;
        }

        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = i == 0 ? pts[i] : pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i + 2 < pts.Count ? pts[i + 2] : pts[i + 1];

            var c1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);
            fig.Segments.Add(new BezierSegment { Point1 = c1, Point2 = c2, Point3 = p2 });
        }

        if (closeArea)
        {
            fig.Segments.Add(new LineSegment { Point = new Point(pts[^1].X, baselineY) });
            fig.IsClosed = true;
        }

        return fig;
    }

    /// <summary>#37: a plain-language week-over-week trend line (this week vs last, busiest day).</summary>
    private void RenderWeekOverWeek(IReadOnlyList<MessengerInstance> instances)
    {
        var wow = MessageAnalyticsService.Instance.GetWeekOverWeek(instances);
        if (!wow.HasData)
        {
            WeekRow.Visibility = Visibility.Collapsed;
            return;
        }

        string trend;
        if (wow.LastWeekTotal == 0)
        {
            trend = "first week of activity";
        }
        else if (wow.DeltaPercent == 0)
        {
            trend = "about the same as last week";
        }
        else
        {
            var dir = wow.DeltaPercent > 0 ? "up" : "down";
            trend = $"{dir} {Math.Abs(wow.DeltaPercent)}% vs last week";
        }

        var busiest = string.IsNullOrEmpty(wow.BusiestDay) ? string.Empty : $" · busiest on {wow.BusiestDay}";
        WeekText.Text = $"This week: {wow.ThisWeekTotal} messages, {trend}{busiest}.";
        WeekRow.Visibility = Visibility.Visible;
    }

    private static Brush Brush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
