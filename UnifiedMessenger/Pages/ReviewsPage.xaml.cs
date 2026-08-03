using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

/// <summary>
/// The Reviews section. A thin host over the existing <c>ReviewHealthPanel</c>, plus the empty state the
/// panel can't provide for itself: it collapses to <see cref="Visibility.Collapsed"/> when there is no
/// Google Business account, which as a dashboard card is right but as a whole page would render blank.
/// </summary>
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

        ReviewHealthPanel.ConfigureServices(_services);
        ReviewHealthPanel.Render();

        var hasGoogleAccount = _services.Registry.Instances.Any(instance =>
            string.Equals(
                PlatformDefinition.NormalizePlatformId(instance.Platform),
                "googlebusiness",
                StringComparison.OrdinalIgnoreCase));

        NoAccountsState.Visibility = hasGoogleAccount ? Visibility.Collapsed : Visibility.Visible;
    }
}
