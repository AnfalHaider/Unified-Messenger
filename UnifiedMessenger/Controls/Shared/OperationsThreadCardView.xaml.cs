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
            new PropertyMetadata(null));

    public static readonly DependencyProperty DisplayStyleProperty =
        DependencyProperty.Register(
            nameof(DisplayStyle),
            typeof(OperationsThreadCardDisplayStyle),
            typeof(OperationsThreadCardView),
            new PropertyMetadata(OperationsThreadCardDisplayStyle.Kanban, OnDisplayStyleChanged));

    public OperationsThreadCardView()
    {
        InitializeComponent();
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

    public Thickness CardPadding { get; private set; } = new(10, 8, 8, 8);

    public double ContentSpacing { get; private set; } = 4;

    public double PlatformIconSize { get; private set; } = 12;

    public double CustomerNameFontSize { get; private set; } = 12;

    public Thickness UrgencyBadgePadding { get; private set; } = new(4, 1, 4, 1);

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
            CardPadding = new Thickness(12);
            ContentSpacing = 6;
            PlatformIconSize = 14;
            CustomerNameFontSize = 14;
            UrgencyBadgePadding = new Thickness(6, 2, 6, 2);
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
            CardPadding = new Thickness(10, 8, 8, 8);
            ContentSpacing = 4;
            PlatformIconSize = 12;
            CustomerNameFontSize = 12;
            UrgencyBadgePadding = new Thickness(4, 1, 4, 1);
            UrgencyBadgeFontSize = 10;
            TagRowVisibility = Visibility.Collapsed;
            SummaryFontSize = 11;
            IsSummaryBold = false;
            SummaryMaxLines = 2;
            SlaStripeVisibility = Visibility.Visible;
            ClickHintVisibility = Visibility.Collapsed;
        }

        Bindings.Update();
    }
}
