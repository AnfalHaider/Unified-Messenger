using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

/// <summary>
/// The Reviews section — a thin host over <c>ReviewDesk</c>, which owns the whole surface.
/// </summary>
/// <remarks>
/// The page previously hosted a separate ReviewHealthPanel (deleted in v4.99.47), and briefly both that and the desk, which meant it
/// showed a queue and then repeated the same figures as per-account cards below it. The desk covers
/// everything that panel did — rating, lifetime total, reply rate, the pending list and the refresh — so it
/// replaced it rather than joining it.
/// </remarks>
public sealed partial class ReviewsPage : Page
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;

    public ReviewsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is RegistryNavigationArgs { Services: { } services })
        {
            _services = services;
        }

        ReviewDesk.ConfigureServices(_services);
        ReviewDesk.Render();

        var hasGoogleAccount = _services.Registry.Instances.Any(instance =>
            string.Equals(
                PlatformDefinition.NormalizePlatformId(instance.Platform),
                "googlebusiness",
                StringComparison.OrdinalIgnoreCase));

        NoAccountsState.Visibility = hasGoogleAccount ? Visibility.Collapsed : Visibility.Visible;
        ReviewDesk.Visibility = hasGoogleAccount ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddAccountButton_Click(object sender, RoutedEventArgs e) =>
        _services.Navigation.RequestAddInstance();
}
