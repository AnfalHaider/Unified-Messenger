using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Controls;

namespace UnifiedMessenger.Controls.Shared;

public enum OperationsThreadCardDisplayStyle
{
    Kanban,
    Immediate
}

public sealed partial class OperationsThreadCardView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(OperationsThreadCardViewModel),
            typeof(OperationsThreadCardView),
            new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty DisplayStyleProperty =
        DependencyProperty.Register(
            nameof(DisplayStyle),
            typeof(OperationsThreadCardDisplayStyle),
            typeof(OperationsThreadCardView),
            new PropertyMetadata(OperationsThreadCardDisplayStyle.Kanban, OnDisplayStyleChanged));

    public OperationsThreadCardView()
    {
        InitializeComponent();
        Loaded += (_, _) => Bindings.Update();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OperationsThreadCardView card)
        {
            card.Bindings.Update();
        }
    }

    public OperationsThreadCardViewModel? ViewModel
    {
        get => (OperationsThreadCardViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public OperationsThreadCardDisplayStyle DisplayStyle
    {
        get => (OperationsThreadCardDisplayStyle)GetValue(DisplayStyleProperty);
        set => SetValue(DisplayStyleProperty, value);
    }

    public Thickness CardPadding { get; private set; } = new(12, 10, 10, 10);

    public double ContentSpacing { get; private set; } = 6;

    public double PlatformIconSize { get; private set; } = 12;

    public double CustomerNameFontSize { get; private set; } = 12;

    public Thickness UrgencyBadgePadding { get; private set; } = new(6, 2, 6, 2);

    public double UrgencyBadgeFontSize { get; private set; } = 10;

    public Visibility TagRowVisibility { get; private set; } = Visibility.Collapsed;

    public double SummaryFontSize { get; private set; } = 11;

    public bool IsSummaryBold { get; private set; }

    public Windows.UI.Text.FontWeight SummaryFontWeight =>
        IsSummaryBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

    public int SummaryMaxLines { get; private set; } = 2;

    public Visibility SlaStripeVisibility { get; private set; } = Visibility.Visible;

    public Visibility ClickHintVisibility { get; private set; } = Visibility.Collapsed;

    private static void OnDisplayStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OperationsThreadCardView card)
        {
            card.ApplyDisplayStyle((OperationsThreadCardDisplayStyle)e.NewValue);
        }
    }

    private void ApplyDisplayStyle(OperationsThreadCardDisplayStyle style)
    {
        if (style == OperationsThreadCardDisplayStyle.Immediate)
        {
            CardPadding = ResolveThickness("UmPaddingThreadCardImmediate", new Thickness(14));
            ContentSpacing = 8;
            PlatformIconSize = 14;
            CustomerNameFontSize = 14;
            UrgencyBadgePadding = ResolveThickness("UmPaddingBadge", new Thickness(6, 2, 6, 2));
            UrgencyBadgeFontSize = 11;
            TagRowVisibility = Visibility.Visible;
            SummaryFontSize = 12;
            IsSummaryBold = true;
            SummaryMaxLines = 0;
            SlaStripeVisibility = Visibility.Collapsed;
            ClickHintVisibility = Visibility.Visible;
        }
        else
        {
            CardPadding = ResolveThickness("UmPaddingThreadCard", new Thickness(12, 10, 10, 10));
            ContentSpacing = 6;
            PlatformIconSize = 12;
            CustomerNameFontSize = 12;
            UrgencyBadgePadding = ResolveThickness("UmPaddingBadge", new Thickness(6, 2, 6, 2));
            UrgencyBadgeFontSize = 10;
            TagRowVisibility = Visibility.Visible;
            SummaryFontSize = 11;
            IsSummaryBold = false;
            SummaryMaxLines = 2;
            SlaStripeVisibility = Visibility.Visible;
            ClickHintVisibility = Visibility.Collapsed;
        }

        Bindings.Update();
    }

    private static Thickness ResolveThickness(string key, Thickness fallback) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is Thickness thickness
            ? thickness
            : fallback;
}
