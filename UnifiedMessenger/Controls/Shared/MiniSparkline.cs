using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace UnifiedMessenger.Controls.Shared;

/// <summary>
/// A very small inline bar sparkline for a KPI tile — a handful of thin bars showing a metric's recent
/// daily trend. Bindable <see cref="Values"/> (oldest→newest); hides itself when there are fewer than two
/// points, so a tile with no accrued history stays clean.
/// </summary>
public sealed class MiniSparkline : ContentControl
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<int>),
        typeof(MiniSparkline),
        new PropertyMetadata(null, OnValuesChanged));

    public MiniSparkline()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        VerticalContentAlignment = VerticalAlignment.Bottom;
        Redraw();
    }

    public IReadOnlyList<int>? Values
    {
        get => (IReadOnlyList<int>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MiniSparkline)d).Redraw();

    private void Redraw()
    {
        var values = Values;
        if (values is null || values.Count < 2)
        {
            Visibility = Visibility.Collapsed;
            Content = null;
            return;
        }

        Visibility = Visibility.Visible;
        var host = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var max = Math.Max(1, values.Max());
        var fill = ResolveBrush("TextFillColorTertiaryBrush");
        foreach (var v in values)
        {
            host.Children.Add(new Rectangle
            {
                Width = 3,
                Height = Math.Max(2, Math.Max(0, v) / (double)max * 14),
                RadiusX = 1,
                RadiusY = 1,
                Fill = fill,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Bottom
            });
        }

        Content = host;
    }

    private static Brush ResolveBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b
            ? b
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
