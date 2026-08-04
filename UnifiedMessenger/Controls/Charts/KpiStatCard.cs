using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls.Charts;

/// <summary>
/// A KPI tile from the mockups: a small icon chip, a label, the big value, a
/// <see cref="DeltaBadge"/> vs the prior period, and an optional sparkline. The reusable building block of
/// the dashboard and analytics KPI rows — one control instead of the ad-hoc metric tiles scattered today.
/// </summary>
public sealed class KpiStatCard : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(KpiStatCard), new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(KpiStatCard), new PropertyMetadata("—", OnChanged));

    public static readonly DependencyProperty IconGlyphProperty = DependencyProperty.Register(
        nameof(IconGlyph), typeof(string), typeof(KpiStatCard), new PropertyMetadata("", OnChanged));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(KpiStatCard), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty DeltaProperty = DependencyProperty.Register(
        nameof(Delta), typeof(MetricDelta), typeof(KpiStatCard), new PropertyMetadata(MetricDelta.None, OnChanged));

    public static readonly DependencyProperty TrendProperty = DependencyProperty.Register(
        nameof(Trend), typeof(IReadOnlyList<int>), typeof(KpiStatCard), new PropertyMetadata(null, OnChanged));

    private readonly FontIcon _icon = new() { FontSize = 14 };
    private readonly Border _iconChip;
    private readonly TextBlock _label = new() { Style = null, FontSize = 12, Opacity = 0.7, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _value = new();
    private readonly DeltaBadge _delta = new();
    private readonly MiniSparkline _spark = new();

    public KpiStatCard()
    {
        IsTabStop = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _value.SetValue(FrameworkElement.StyleProperty, TryStyle("UmMetricValueStyle"));

        _iconChip = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = TryCorner("UmCornerRadiusSmValue", new CornerRadius(6)),
            Background = ResolveBrush("SystemFillColorAttentionBackgroundBrush"),
            Child = _icon,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_label, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_iconChip, 1);
        header.Children.Add(_label);
        header.Children.Add(_iconChip);

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(header);
        body.Children.Add(_value);
        body.Children.Add(_delta);
        body.Children.Add(_spark);

        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = TryCorner("UmCornerRadiusMdValue", new CornerRadius(8)),
            Background = ResolveBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ResolveBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Child = body
        };

        Content = card;
        Apply();
    }

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string IconGlyph { get => (string)GetValue(IconGlyphProperty); set => SetValue(IconGlyphProperty, value); }
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public MetricDelta Delta { get => (MetricDelta)GetValue(DeltaProperty); set => SetValue(DeltaProperty, value); }
    public IReadOnlyList<int>? Trend { get => (IReadOnlyList<int>?)GetValue(TrendProperty); set => SetValue(TrendProperty, value); }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((KpiStatCard)d).Apply();

    private void Apply()
    {
        _label.Text = Label;
        _value.Text = Value;
        _icon.Glyph = IconGlyph;
        _icon.Foreground = AccentBrush ?? ResolveBrush("SystemFillColorAttentionBrush");
        _delta.Delta = Delta;
        _spark.Values = Trend;

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, $"{Label}: {Value}");
    }

    private static Style? TryStyle(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) ? v as Style : null;

    private static CornerRadius TryCorner(string key, CornerRadius fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is CornerRadius c ? c : fallback;

    private static Brush ResolveBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b
            ? b
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
}
