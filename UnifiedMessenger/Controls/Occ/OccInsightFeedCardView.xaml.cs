using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Occ;

public sealed partial class OccInsightFeedCardView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(OperationsInsightFeedViewModel),
            typeof(OccInsightFeedCardView),
            new PropertyMetadata(null));

    public OccInsightFeedCardView()
    {
        InitializeComponent();
    }

    public OperationsInsightFeedViewModel? ViewModel
    {
        get => (OperationsInsightFeedViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
