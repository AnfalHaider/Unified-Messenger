using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls.Shared;

public sealed partial class SectionHeaderView : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(SectionHeaderView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(
            nameof(BadgeText),
            typeof(string),
            typeof(SectionHeaderView),
            new PropertyMetadata(string.Empty, OnBadgeTextChanged));

    public SectionHeaderView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public static readonly DependencyProperty BadgeVisibilityProperty =
        DependencyProperty.Register(
            nameof(BadgeVisibility),
            typeof(Visibility),
            typeof(SectionHeaderView),
            new PropertyMetadata(Visibility.Collapsed));

    public Visibility BadgeVisibility
    {
        get => (Visibility)GetValue(BadgeVisibilityProperty);
        private set => SetValue(BadgeVisibilityProperty, value);
    }

    private static void OnBadgeTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SectionHeaderView header)
        {
            header.BadgeVisibility = string.IsNullOrWhiteSpace((string?)e.NewValue)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}
