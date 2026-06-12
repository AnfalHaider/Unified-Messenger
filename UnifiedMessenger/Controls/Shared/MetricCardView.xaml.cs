using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Shared;

public sealed partial class MetricCardView : UserControl
{
    public static readonly DependencyProperty IsAccentProperty =
        DependencyProperty.Register(
            nameof(IsAccent),
            typeof(bool),
            typeof(MetricCardView),
            new PropertyMetadata(false, OnIsAccentChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata(string.Empty, OnMetricTextChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata("—", OnMetricTextChanged));

    public static readonly DependencyProperty MetricAutomationIdProperty =
        DependencyProperty.Register(
            nameof(MetricAutomationId),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata(string.Empty, OnMetricAutomationIdChanged));

    public static readonly DependencyProperty SubtextProperty =
        DependencyProperty.Register(
            nameof(Subtext),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata(string.Empty, OnSubtextChanged));

    public MetricCardView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateAccentVisualState();
            UpdateAutomationName();
        };
    }

    public string MetricAutomationId
    {
        get => (string)GetValue(MetricAutomationIdProperty);
        set => SetValue(MetricAutomationIdProperty, value);
    }

    public bool IsAccent
    {
        get => (bool)GetValue(IsAccentProperty);
        set => SetValue(IsAccentProperty, value);
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

    private static void OnIsAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.UpdateAccentVisualState();
        }
    }

    private void UpdateAccentVisualState()
    {
        VisualStateManager.GoToState(this, IsAccent ? "Accent" : "Normal", true);
    }

    private static void OnMetricTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.UpdateAutomationName();
        }
    }

    private static void OnMetricAutomationIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.ApplyMetricAutomationId();
        }
    }

    private void ApplyMetricAutomationId()
    {
        if (!string.IsNullOrWhiteSpace(MetricAutomationId))
        {
            AutomationProperties.SetAutomationId(this, MetricAutomationId);
        }
    }

    private void UpdateAutomationName()
    {
        ApplyMetricAutomationId();
        var label = string.IsNullOrWhiteSpace(Label) ? "Metric" : Label.Trim();
        var value = string.IsNullOrWhiteSpace(Value) ? "—" : Value.Trim();
        AutomationProperties.SetName(this, $"{label}: {value}");
    }
}
