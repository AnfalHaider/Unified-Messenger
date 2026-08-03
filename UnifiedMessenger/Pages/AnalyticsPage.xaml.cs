using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

/// <summary>
/// The Analytics section. A thin host over the existing <c>ActivityPatternsPanel</c> — the panel already
/// owns the account/range filters, the hour/day/month dimensions, the heat map and the week-over-week
/// comparison, so this page exists to give it a home in the left navigation rather than to reimplement it.
/// </summary>
public sealed partial class AnalyticsPage : Page
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;

    public AnalyticsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is RegistryNavigationArgs { Services: { } services })
        {
            _services = services;
        }

        ActivityPatternsPanel.ConfigureServices(_services);
        ActivityPatternsPanel.Render();
    }
}
