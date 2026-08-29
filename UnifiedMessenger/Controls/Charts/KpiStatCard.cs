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

    private readonly FontIcon _icon = new() { FontSize = UmScale.Icon.Md };
    private readonly Border _iconChip;
    private readonly Border _card;
    private readonly TextBlock _label = new() { Style = null, FontSize = UmScale.Icon.Sm, Opacity = 0.7, TextTrimming = TextTrimming.CharacterEllipsis };
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

        _card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = TryCorner("UmCornerRadiusMdValue", new CornerRadius(8)),
            BorderThickness = new Thickness(1),
            Child = body
        };

        Content = _card;

        // Brushes are applied here AND on ActualThemeChanged/Loaded, never once in the constructor.
        // A control being constructed is not yet in the visual tree, so its ActualTheme is still Default
        // and the theme cannot be known — and this app applies its theme on the window root rather than at
        // application level, so Application.Current.RequestedTheme reads Light even in dark mode. Resolving
        // once at construction therefore baked LIGHT surfaces into these four tiles permanently: a pale
        // grey card carrying white text, on the Analytics page's headline metrics. Every other imperative
        // control escaped this only because it rebuilds its content after being parented; this one builds
        // once and afterwards only updates text.
        ApplyThemeBrushes();
        ActualThemeChanged += (_, _) => ApplyThemeBrushes();
        Loaded += (_, _) => ApplyThemeBrushes();
        Apply();
    }

    private void ApplyThemeBrushes()
    {
        _card.Background = ResolveBrush("CardBackgroundFillColorDefaultBrush");
        _card.BorderBrush = ResolveBrush("UmHairlineBrush");
        _iconChip.Background = ResolveBrush("SystemFillColorAttentionBackgroundBrush");
        _icon.Foreground = AccentBrush ?? ResolveBrush("SystemFillColorAttentionBrush");
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
        // The icon foreground depends on AccentBrush, which this can change, so re-run the themed brushes
        // rather than duplicating the fallback here — the palette ratchet counts that duplicate.
        ApplyThemeBrushes();
        _delta.Delta = Delta;
        _spark.Values = Trend;

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, $"{Label}: {Value}");
    }

    private static Style? TryStyle(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) ? v as Style : null;

    private static CornerRadius TryCorner(string key, CornerRadius fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is CornerRadius c ? c : fallback;

    // Was a private static lookup straight into Application.Current.Resources, which resolves the
    // APP-default theme rather than this element's — so in dark theme these four tiles drew
    // CardBackgroundFillColorDefault's LIGHT value: a pale grey card with white text on it, and a
    // label at 0.7 opacity that was effectively invisible. Observed on the Analytics page.
    private Brush ResolveBrush(string key) => Services.ThemeBrushResolver.Resolve(this, key);
}
