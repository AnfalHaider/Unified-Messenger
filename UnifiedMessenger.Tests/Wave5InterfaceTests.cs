using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests;

public class Wave5InterfaceTests
{
    [Fact]
    public void ApplicationServices_DefaultsWireSingletonImplementations()
    {
        var services = ApplicationServices.CreateDefault();

        Assert.Same(InstanceSessionManager.Instance, services.SessionManager);
        Assert.Same(ShellNavigationService.Instance, services.Navigation);
        Assert.Same(NotificationHub.Instance, services.NotificationHub);
        Assert.Same(AppSettingsService.Instance, services.AppSettings);
        Assert.Same(ThreadRegistryService.Instance, services.ThreadRegistry);
        Assert.Same(MessageTriageService.Instance, services.MessageTriage);
        Assert.Same(MessageAnalyticsService.Instance, services.MessageAnalytics);
        Assert.Same(WebViewScriptGateway.Instance, services.WebViewScriptGateway);
        Assert.Same(GitHubUpdateService.Instance, services.GitHubUpdate);
        Assert.Same(RichTriageStoreService.Instance, services.RichTriageStore);
        Assert.Same(WebViewProfileManager.Instance, services.WebViewProfileManager);
        Assert.Same(SystemTrayService.Instance, services.SystemTray);
        Assert.Same(GlobalHotkeyService.Instance, services.GlobalHotkey);
        Assert.Same(AppNotificationService.Instance, services.AppNotification);
        Assert.Same(TaskbarBadgeService.Instance, services.TaskbarBadge);
        Assert.Same(OllamaOrchestrationService.Instance, services.Ollama);
        Assert.IsType<InstanceRegistryService>(services.Registry);
        Assert.IsType<WinUiDialogService>(services.Dialog);
    }

    [Fact]
    public void ApplicationServices_CreateDefault_MatchesParameterlessConstructor()
    {
        var fromFactory = ApplicationServices.CreateDefault();
        var fromConstructor = new ApplicationServices();

        Assert.Same(fromConstructor.SessionManager, fromFactory.SessionManager);
        Assert.Same(fromConstructor.AppNotification, fromFactory.AppNotification);
        Assert.Same(fromConstructor.RichTriageStore, fromFactory.RichTriageStore);
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
        Assert.IsAssignableFrom<IRichTriageStoreService>(RichTriageStoreService.Instance);
        Assert.IsAssignableFrom<IWebViewProfileManager>(WebViewProfileManager.Instance);
        Assert.IsAssignableFrom<ISystemTrayService>(SystemTrayService.Instance);
        Assert.IsAssignableFrom<IGlobalHotkeyService>(GlobalHotkeyService.Instance);
        Assert.IsAssignableFrom<IAppNotificationService>(AppNotificationService.Instance);
        Assert.IsAssignableFrom<ITaskbarBadgeService>(TaskbarBadgeService.Instance);
        Assert.IsAssignableFrom<IOllamaOrchestrationService>(OllamaOrchestrationService.Instance);
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
