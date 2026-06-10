using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Shared;

public sealed partial class SurfaceCard : UserControl
{
    public static readonly DependencyProperty CardBodyProperty =
        DependencyProperty.Register(
            nameof(CardBody),
            typeof(object),
            typeof(SurfaceCard),
            new PropertyMetadata(null));

    public SurfaceCard()
    {
        InitializeComponent();
    }

    public object? CardBody
    {
        get => GetValue(CardBodyProperty);
        set => SetValue(CardBodyProperty, value);
    }
}
