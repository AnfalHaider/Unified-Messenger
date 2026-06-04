using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RefreshContent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshNavigationChrome();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshNavigationChrome();
    }

    private void RefreshContent()
    {
        VersionText.Text = AboutPageHelper.BuildAboutVersionLabel(typeof(App).Assembly.GetName().Version);
    }

    private void RefreshNavigationChrome()
    {
        BackLink.Visibility = AboutPageHelper.ShouldShowBackLink(Frame?.CanGoBack == true)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void BackLink_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
    }
}
