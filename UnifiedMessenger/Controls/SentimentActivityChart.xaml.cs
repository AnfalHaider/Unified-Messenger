using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class SentimentActivityChart : UserControl
{
    public SentimentActivityChartViewModel ViewModel { get; } = new();

    public SentimentActivityChart()
    {
        InitializeComponent();
    }

    public void SetSeries(MessageTriageDashboardSnapshot? snapshot)
    {
        ViewModel.ApplySeries(snapshot);
        SummaryText.Text = ViewModel.SummaryText;
        ChartAccessibilityHost.Summary = ViewModel.SummaryText;
        EmptyHint.Visibility = ViewModel.ShowEmptyHint ? Visibility.Visible : Visibility.Collapsed;
        ChartHost.Visibility = ViewModel.ShowEmptyHint ? Visibility.Collapsed : Visibility.Visible;
        RenderBarsFromViewModel();
    }

    private void RenderBarsFromViewModel()
    {
        ChartBarRenderHelper.PrepareChartHost(ChartHost, ViewModel.Bars.Count);

        var positiveBrush = Application.Current.Resources["SystemFillColorSuccessBrush"] as Brush;
        var neutralBrush = Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush;
        var negativeBrush = Application.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

        for (var i = 0; i < ViewModel.Bars.Count; i++)
        {
            var bar = ViewModel.Bars[i];
            var column = ChartBarRenderHelper.CreateColumnStack(bar.ToolTipText);

            var bars = new Grid { Height = 90, VerticalAlignment = VerticalAlignment.Bottom };
            bars.ColumnDefinitions.Add(new ColumnDefinition());
            bars.ColumnDefinitions.Add(new ColumnDefinition());
            bars.ColumnDefinitions.Add(new ColumnDefinition());

            var positiveBar = ChartBarRenderHelper.CreateBar(bar.PositiveBarHeight, positiveBrush, new Thickness(0, 0, 2, 0));
            var neutralBar = ChartBarRenderHelper.CreateBar(bar.NeutralBarHeight, neutralBrush, new Thickness(2, 0, 2, 0));
            var negativeBar = ChartBarRenderHelper.CreateBar(bar.NegativeBarHeight, negativeBrush, new Thickness(2, 0, 0, 0));

            Grid.SetColumn(positiveBar, 0);
            Grid.SetColumn(neutralBar, 1);
            Grid.SetColumn(negativeBar, 2);
            bars.Children.Add(positiveBar);
            bars.Children.Add(neutralBar);
            bars.Children.Add(negativeBar);

            column.Children.Add(bars);

            if (!string.IsNullOrWhiteSpace(bar.DayTotalText))
            {
                column.Children.Add(ChartBarRenderHelper.CreateLabel(bar.DayTotalText, semiBold: true));
            }

            column.Children.Add(ChartBarRenderHelper.CreateLabel(bar.Label, opacity: 0.65));
            ChartBarRenderHelper.AddColumn(ChartHost, i, column);
        }
    }
}
