using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class SentimentActivityChart : UserControl
{
    public SentimentActivityChart()
    {
        InitializeComponent();
    }

    public void SetSeries(MessageTriageDashboardSnapshot? snapshot)
    {
        ChartHost.Children.Clear();
        ChartHost.ColumnDefinitions.Clear();

        snapshot ??= MessageTriageDashboardSnapshot.Empty;
        SummaryText.Text = DashboardTriageHelper.FormatSentimentSummary(snapshot);

        var series = snapshot.WeeklySentiment;
        if (series.Count == 0 ||
            series.All(point => point.Positive + point.Neutral + point.Negative == 0))
        {
            EmptyHint.Visibility = Visibility.Visible;
            return;
        }

        EmptyHint.Visibility = Visibility.Collapsed;

        foreach (var _ in series)
        {
            ChartHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var max = DashboardTriageHelper.ComputeSentimentScaleMaximum(series);
        const double maxHeight = 90;
        var positiveBrush = Application.Current.Resources["SystemFillColorSuccessBrush"] as Brush;
        var neutralBrush = Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush;
        var negativeBrush = Application.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

        for (var i = 0; i < series.Count; i++)
        {
            var point = series[i];
            var column = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 4
            };
            ToolTipService.SetToolTip(
                column,
                $"{point.Label}: {point.Positive} positive, {point.Neutral} neutral, {point.Negative} negative");

            var bars = new Grid { Height = 90, VerticalAlignment = VerticalAlignment.Bottom };
            bars.ColumnDefinitions.Add(new ColumnDefinition());
            bars.ColumnDefinitions.Add(new ColumnDefinition());
            bars.ColumnDefinitions.Add(new ColumnDefinition());

            var positiveBar = CreateBar(
                DashboardTriageHelper.ComputeBarHeight(point.Positive, max, maxHeight),
                positiveBrush,
                new Thickness(0, 0, 2, 0));
            var neutralBar = CreateBar(
                DashboardTriageHelper.ComputeBarHeight(point.Neutral, max, maxHeight),
                neutralBrush,
                new Thickness(2, 0, 2, 0));
            var negativeBar = CreateBar(
                DashboardTriageHelper.ComputeBarHeight(point.Negative, max, maxHeight),
                negativeBrush,
                new Thickness(2, 0, 0, 0));

            Grid.SetColumn(positiveBar, 0);
            Grid.SetColumn(neutralBar, 1);
            Grid.SetColumn(negativeBar, 2);
            bars.Children.Add(positiveBar);
            bars.Children.Add(neutralBar);
            bars.Children.Add(negativeBar);

            column.Children.Add(bars);

            var dayTotal = point.Positive + point.Neutral + point.Negative;
            if (dayTotal > 0)
            {
                column.Children.Add(new TextBlock
                {
                    Text = dayTotal.ToString(),
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            column.Children.Add(new TextBlock
            {
                Text = point.Label,
                FontSize = 10,
                Opacity = 0.65,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            Grid.SetColumn(column, i);
            ChartHost.Children.Add(column);
        }
    }

    private static Border CreateBar(double height, Brush? brush, Thickness margin) =>
        new()
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = height,
            Margin = margin,
            Background = brush,
            CornerRadius = new CornerRadius(3, 3, 0, 0)
        };
}
