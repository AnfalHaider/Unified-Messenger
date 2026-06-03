using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace UnifiedMessenger.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        var version = typeof(App).Assembly.GetName().Version;
        VersionText.Text = version is null
            ? "Unified Messenger v1.0.0"
            : $"Unified Messenger v{version.Major}.{version.Minor}.{version.Build}";
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        BackLink.Visibility = Frame?.CanGoBack == true
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
