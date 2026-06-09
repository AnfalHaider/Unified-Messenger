using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Shared;

public sealed partial class MetricCardView : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata("—"));

    public static readonly DependencyProperty SubtextProperty =
        DependencyProperty.Register(
            nameof(Subtext),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata(string.Empty, OnSubtextChanged));

    public MetricCardView()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Subtext
    {
        get => (string)GetValue(SubtextProperty);
        set => SetValue(SubtextProperty, value);
    }

    public static readonly DependencyProperty SubtextVisibilityProperty =
        DependencyProperty.Register(
            nameof(SubtextVisibility),
            typeof(Visibility),
            typeof(MetricCardView),
            new PropertyMetadata(Visibility.Collapsed));

    public Visibility SubtextVisibility
    {
        get => (Visibility)GetValue(SubtextVisibilityProperty);
        private set => SetValue(SubtextVisibilityProperty, value);
    }

    private static void OnSubtextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.SubtextVisibility = string.IsNullOrWhiteSpace((string?)e.NewValue)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}
