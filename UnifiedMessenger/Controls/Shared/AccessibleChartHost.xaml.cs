using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Shared;

public sealed partial class AccessibleChartHost : UserControl
{
    public static readonly DependencyProperty ChartContentProperty =
        DependencyProperty.Register(
            nameof(ChartContent),
            typeof(object),
            typeof(AccessibleChartHost),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(
            nameof(Summary),
            typeof(string),
            typeof(AccessibleChartHost),
            new PropertyMetadata(string.Empty, OnSummaryChanged));

    public AccessibleChartHost()
    {
        InitializeComponent();
    }

    public object? ChartContent
    {
        get => GetValue(ChartContentProperty);
        set => SetValue(ChartContentProperty, value);
    }

    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    private static void OnSummaryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AccessibleChartHost host)
        {
            var summary = e.NewValue as string ?? string.Empty;
            AutomationProperties.SetName(host, summary);
            ToolTipService.SetToolTip(host, summary);
        }
    }
}
