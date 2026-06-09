using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace UnifiedMessenger.Controls.Occ;

internal static class ChartBarRenderHelper
{
    public static void PrepareChartHost(Grid chartHost, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(chartHost);

        chartHost.Children.Clear();
        chartHost.ColumnDefinitions.Clear();

        for (var index = 0; index < columnCount; index++)
        {
            chartHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
    }

    public static void AddColumn(Grid chartHost, int columnIndex, FrameworkElement columnContent)
    {
        Grid.SetColumn(columnContent, columnIndex);
        chartHost.Children.Add(columnContent);
    }

    public static StackPanel CreateColumnStack(string? toolTipText = null)
    {
        var column = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 4
        };

        if (!string.IsNullOrWhiteSpace(toolTipText))
        {
            ToolTipService.SetToolTip(column, toolTipText);
        }

        return column;
    }

    public static Border CreateBar(double height, Brush? brush, Thickness margin) =>
        new()
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = height,
            Margin = margin,
            Background = brush,
            CornerRadius = new CornerRadius(3, 3, 0, 0)
        };

    public static TextBlock CreateLabel(string text, double opacity = 0.6, bool semiBold = false) =>
        new()
        {
            Text = text,
            FontSize = 10,
            Opacity = opacity,
            FontWeight = semiBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
}
