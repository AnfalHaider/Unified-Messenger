using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class Wave5InterfaceTests
{
    [Fact]
    public void ApplicationServices_DefaultsWireSingletonImplementations()
    {
        var services = new ApplicationServices();

        Assert.Same(InstanceSessionManager.Instance, services.SessionManager);
        Assert.Same(ShellNavigationService.Instance, services.Navigation);
        Assert.Same(NotificationHub.Instance, services.NotificationHub);
        Assert.Same(AppSettingsService.Instance, services.AppSettings);
        Assert.Same(ThreadRegistryService.Instance, services.ThreadRegistry);
        Assert.Same(MessageTriageService.Instance, services.MessageTriage);
        Assert.Same(MessageAnalyticsService.Instance, services.MessageAnalytics);
        Assert.Same(WebViewScriptGateway.Instance, services.WebViewScriptGateway);
        Assert.Same(GitHubUpdateService.Instance, services.GitHubUpdate);
        Assert.IsType<InstanceRegistryService>(services.Registry);
        Assert.IsType<WinUiDialogService>(services.Dialog);
    }

    [Fact]
    public void CoreSingletons_ImplementWave5Contracts()
    {
        Assert.IsAssignableFrom<IAppSettingsService>(AppSettingsService.Instance);
        Assert.IsAssignableFrom<IThreadRegistryService>(ThreadRegistryService.Instance);
        Assert.IsAssignableFrom<IMessageTriageService>(MessageTriageService.Instance);
        Assert.IsAssignableFrom<IInstanceSessionManager>(InstanceSessionManager.Instance);
        Assert.IsAssignableFrom<INavigationService>(ShellNavigationService.Instance);
        Assert.IsAssignableFrom<INotificationHubService>(NotificationHub.Instance);
        Assert.IsAssignableFrom<IMessageAnalyticsService>(MessageAnalyticsService.Instance);
        Assert.IsAssignableFrom<IGitHubUpdateService>(GitHubUpdateService.Instance);
        Assert.IsAssignableFrom<IWebViewScriptGateway>(WebViewScriptGateway.Instance);
    }

    [Fact]
    public void InstanceRegistryService_ImplementsRegistryContract()
    {
        var registry = new InstanceRegistryService();
        Assert.IsAssignableFrom<IInstanceRegistryService>(registry);
    }

    [Fact]
    public void ApplicationServices_AcceptsInjectedRegistry()
    {
        var registry = new InstanceRegistryService();
        var services = new ApplicationServices(registry: registry);

        Assert.Same(registry, services.Registry);
    }

    [Fact]
    public void ApplicationServices_ConfigureUi_DeferredProvider_DoesNotThrowWhenRootUnavailable()
    {
        var services = new ApplicationServices();
        var exception = Record.Exception(() => services.ConfigureUi(() => null!));
        Assert.Null(exception);
    }
}
