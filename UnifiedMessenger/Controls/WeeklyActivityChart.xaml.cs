using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class WeeklyActivityChart : UserControl
{
    public WeeklyActivityChartViewModel ViewModel { get; } = new();

    public WeeklyActivityChart()
    {
        InitializeComponent();
    }

    public void SetSeries(IReadOnlyList<DailyActivityPoint>? series)
    {
        ViewModel.ApplySeries(series);
        SummaryText.Text = ViewModel.SummaryText;
        ChartAccessibilityHost.Summary = ViewModel.SummaryText;
        EmptyHint.Visibility = ViewModel.ShowEmptyHint ? Visibility.Visible : Visibility.Collapsed;
        ChartHost.Visibility = ViewModel.ShowEmptyHint ? Visibility.Collapsed : Visibility.Visible;
        RenderBarsFromViewModel();
    }

    private void RenderBarsFromViewModel()
    {
        ChartBarRenderHelper.PrepareChartHost(ChartHost, ViewModel.Bars.Count);

        var accentBrush = Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush;
        var receivedBrush = Application.Current.Resources["SystemFillColorSuccessBrush"] as Brush;

        for (var i = 0; i < ViewModel.Bars.Count; i++)
        {
            var bar = ViewModel.Bars[i];
            var column = ChartBarRenderHelper.CreateColumnStack(bar.ToolTipText);

            var bars = new Grid { Height = 100, VerticalAlignment = VerticalAlignment.Bottom };
            bars.ColumnDefinitions.Add(new ColumnDefinition());
            bars.ColumnDefinitions.Add(new ColumnDefinition());

            var sentBar = ChartBarRenderHelper.CreateBar(bar.SentBarHeight, accentBrush, new Thickness(0, 0, 2, 0));
            var receivedBar = ChartBarRenderHelper.CreateBar(bar.ReceivedBarHeight, receivedBrush, new Thickness(2, 0, 0, 0));

            Grid.SetColumn(sentBar, 0);
            Grid.SetColumn(receivedBar, 1);
            bars.Children.Add(sentBar);
            bars.Children.Add(receivedBar);

            column.Children.Add(bars);
            column.Children.Add(ChartBarRenderHelper.CreateLabel(bar.Label));
            ChartBarRenderHelper.AddColumn(ChartHost, i, column);
        }
    }
}
