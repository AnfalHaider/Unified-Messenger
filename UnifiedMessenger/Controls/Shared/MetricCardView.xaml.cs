using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace UnifiedMessenger.Controls.Shared;

public sealed partial class MetricCardView : UserControl
{
    public static readonly DependencyProperty IsAccentProperty =
        DependencyProperty.Register(
            nameof(IsAccent),
            typeof(bool),
            typeof(MetricCardView),
            new PropertyMetadata(false, OnIsAccentChanged));

    public static readonly DependencyProperty IsInteractiveProperty =
        DependencyProperty.Register(
            nameof(IsInteractive),
            typeof(bool),
            typeof(MetricCardView),
            new PropertyMetadata(false, OnIsInteractiveChanged));

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

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(
            nameof(IconGlyph),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata(string.Empty, OnIconGlyphChanged));

    public static readonly DependencyProperty NavigationTooltipProperty =
        DependencyProperty.Register(
            nameof(NavigationTooltip),
            typeof(string),
            typeof(MetricCardView),
            new PropertyMetadata(string.Empty, OnNavigationTooltipChanged));

    private bool _isPressed;

    public MetricCardView()
    {
        InitializeComponent();
        RegisterPropertyChangedCallback(IsEnabledProperty, (_, _) => UpdateInteractionVisualState());
        Loaded += (_, _) =>
        {
            UpdateAccentVisualState();
            UpdateInteractionVisualState();
            UpdateAutomationName();
        };
    }

    public event EventHandler<TappedRoutedEventArgs>? Activated;

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

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
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

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string NavigationTooltip
    {
        get => (string)GetValue(NavigationTooltipProperty);
        set => SetValue(NavigationTooltipProperty, value);
    }

    public Visibility IconVisibility { get; private set; } = Visibility.Collapsed;

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

    private void CardBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanActivate())
        {
            return;
        }

        Activated?.Invoke(this, e);
        e.Handled = true;
    }

    private void CardBorder_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!CanActivate())
        {
            return;
        }

        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            RaiseActivated();
            e.Handled = true;
        }
    }

    private void RaiseActivated()
    {
        if (!CanActivate())
        {
            return;
        }

        Activated?.Invoke(this, new TappedRoutedEventArgs());
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        if (CanActivate())
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            VisualStateManager.GoToState(this, "PointerOver", true);
        }
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        _isPressed = false;
        UpdateInteractionVisualState();
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!CanActivate())
        {
            return;
        }

        _isPressed = true;
        VisualStateManager.GoToState(this, "Pressed", true);
        CapturePointer(e.Pointer);
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!CanActivate())
        {
            return;
        }

        ReleasePointerCapture(e.Pointer);
        _isPressed = false;
        UpdateInteractionVisualState();
    }

    private bool CanActivate() => IsInteractive && IsEnabled;

    private static void OnSubtextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.SubtextVisibility = string.IsNullOrWhiteSpace((string?)e.NewValue)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private static void OnIconGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.IconVisibility = string.IsNullOrWhiteSpace((string?)e.NewValue)
                ? Visibility.Collapsed
                : Visibility.Visible;
            card.Bindings.Update();
        }
    }

    private static void OnIsAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.UpdateAccentVisualState();
        }
    }

    private static void OnIsInteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            card.IsTabStop = card.IsInteractive;
            card.UpdateInteractionVisualState();
            card.UpdateAutomationName();
        }
    }

    private static void OnNavigationTooltipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricCardView card)
        {
            ToolTipService.SetToolTip(card, string.IsNullOrWhiteSpace(card.NavigationTooltip) ? null : card.NavigationTooltip);
        }
    }

    private void UpdateAccentVisualState()
    {
        VisualStateManager.GoToState(this, IsAccent ? "Accent" : "Normal", true);
    }

    private void UpdateInteractionVisualState()
    {
        if (!IsInteractive)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            VisualStateManager.GoToState(this, "InteractiveNormal", true);
            return;
        }

        if (!IsEnabled)
        {
            VisualStateManager.GoToState(this, "Disabled", true);
            return;
        }

        VisualStateManager.GoToState(this, _isPressed ? "Pressed" : "InteractiveNormal", true);
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

        if (IsInteractive)
        {
            AutomationProperties.SetHelpText(
                this,
                string.IsNullOrWhiteSpace(NavigationTooltip)
                    ? "Opens the highest-priority thread for this metric."
                    : NavigationTooltip);
        }
        else
        {
            AutomationProperties.SetHelpText(this, string.Empty);
        }
    }
}
