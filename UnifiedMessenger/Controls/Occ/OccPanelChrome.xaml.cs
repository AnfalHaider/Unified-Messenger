using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Occ;

public sealed partial class OccPanelChrome : UserControl
{
    public static readonly DependencyProperty PanelIdProperty =
        DependencyProperty.Register(nameof(PanelId), typeof(string), typeof(OccPanelChrome), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register(nameof(PanelTitle), typeof(string), typeof(OccPanelChrome), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PanelContentProperty =
        DependencyProperty.Register(nameof(PanelContent), typeof(object), typeof(OccPanelChrome), new PropertyMetadata(null));

    public static readonly DependencyProperty IsEditModeProperty =
        DependencyProperty.Register(
            nameof(IsEditMode),
            typeof(bool),
            typeof(OccPanelChrome),
            new PropertyMetadata(false, OnIsEditModeChanged));

    public event EventHandler<string>? HideRequested;

    public event EventHandler<(string PanelId, int DeltaColumns)>? ResizeRequested;

    public OccPanelChrome()
    {
        InitializeComponent();
    }

    public string PanelId
    {
        get => (string)GetValue(PanelIdProperty);
        set => SetValue(PanelIdProperty, value);
    }

    public string PanelTitle
    {
        get => (string)GetValue(PanelTitleProperty);
        set => SetValue(PanelTitleProperty, value);
    }

    public object? PanelContent
    {
        get => GetValue(PanelContentProperty);
        set => SetValue(PanelContentProperty, value);
    }

    public bool IsEditMode
    {
        get => (bool)GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    private static void OnIsEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OccPanelChrome chrome)
        {
            chrome.ChromeHeader.Visibility = e.NewValue is true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void HidePanelButton_Click(object sender, RoutedEventArgs e) =>
        HideRequested?.Invoke(this, PanelId);

    private void ShrinkWidthButton_Click(object sender, RoutedEventArgs e) =>
        ResizeRequested?.Invoke(this, (PanelId, -1));

    private void GrowWidthButton_Click(object sender, RoutedEventArgs e) =>
        ResizeRequested?.Invoke(this, (PanelId, 1));
}
