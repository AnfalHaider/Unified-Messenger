using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class WeeklyActivityChart : UserControl
{
    public WeeklyActivityChart()
    {
        InitializeComponent();
    }

    public void SetSeries(IReadOnlyList<DailyActivityPoint>? series)
    {
        ChartHost.Children.Clear();
        ChartHost.ColumnDefinitions.Clear();

        var summary = WeeklyActivityChartHelper.BuildSummary(series);
        SummaryText.Text = summary.SummaryText;

        if (series is null || series.Count == 0)
        {
            EmptyHint.Visibility = Visibility.Visible;
            return;
        }

        EmptyHint.Visibility = Visibility.Collapsed;

        foreach (var _ in series)
        {
            ChartHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var max = WeeklyActivityChartHelper.ComputeScaleMaximum(series);
        const double maxHeight = 100;
        var accentBrush = Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush;
        var receivedBrush = Application.Current.Resources["SystemFillColorSuccessBrush"] as Brush;

        for (var i = 0; i < series.Count; i++)
        {
            var point = series[i];
            var sentHeight = WeeklyActivityChartHelper.ComputeBarHeight(point.Sent, max, maxHeight);
            var receivedHeight = WeeklyActivityChartHelper.ComputeBarHeight(point.Received, max, maxHeight);
            var dailyTotal = point.Sent + point.Received;

            var column = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 4
            };
            ToolTipService.SetToolTip(
                column,
                $"{point.Label}: {point.Sent} sent, {point.Received} received ({dailyTotal} total)");

            var bars = new Grid { Height = 100, VerticalAlignment = VerticalAlignment.Bottom };
            bars.ColumnDefinitions.Add(new ColumnDefinition());
            bars.ColumnDefinitions.Add(new ColumnDefinition());

            var sentBar = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = sentHeight,
                Margin = new Thickness(0, 0, 2, 0),
                Background = accentBrush,
                CornerRadius = new CornerRadius(3, 3, 0, 0)
            };
            var receivedBar = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = receivedHeight,
                Margin = new Thickness(2, 0, 0, 0),
                Background = receivedBrush,
                CornerRadius = new CornerRadius(3, 3, 0, 0)
            };

            Grid.SetColumn(sentBar, 0);
            Grid.SetColumn(receivedBar, 1);
            bars.Children.Add(sentBar);
            bars.Children.Add(receivedBar);

            column.Children.Add(bars);
            column.Children.Add(new TextBlock
            {
                Text = point.Label,
                FontSize = 10,
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            Grid.SetColumn(column, i);
            ChartHost.Children.Add(column);
        }
    }
}
