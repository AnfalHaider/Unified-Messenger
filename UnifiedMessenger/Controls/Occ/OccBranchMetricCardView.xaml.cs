using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Occ;

public sealed partial class OccBranchMetricCardView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(BranchMetricViewModel),
            typeof(OccBranchMetricCardView),
            new PropertyMetadata(null));

    public OccBranchMetricCardView()
    {
        InitializeComponent();
    }

    public BranchMetricViewModel? ViewModel
    {
        get => (BranchMetricViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
