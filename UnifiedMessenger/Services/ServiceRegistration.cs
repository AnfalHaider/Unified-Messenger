using Microsoft.Extensions.DependencyInjection;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Interfaced services — two are constructed fresh per container (no shared static singleton):
        //   InstanceRegistryService owns the on-disk instance store for this process lifetime.
        //   WinUiDialogService holds a XamlRoot provider set after the window opens.
        services.AddSingleton<IInstanceRegistryService, InstanceRegistryService>();
        services.AddSingleton<IDialogService, WinUiDialogService>();

        // All remaining services delegate to their existing static singletons.
        services.AddSingleton<IInstanceSessionManager>(_ => InstanceSessionManager.Instance);
        services.AddSingleton<INavigationService>(_ => ShellNavigationService.Instance);
        services.AddSingleton<INotificationHubService>(_ => NotificationHub.Instance);
        services.AddSingleton<IAppSettingsService>(_ => AppSettingsService.Instance);
        services.AddSingleton<IThreadRegistryService>(_ => ThreadRegistryService.Instance);
        services.AddSingleton<IMessageTriageService>(_ => MessageTriageService.Instance);
        services.AddSingleton<IMessageAnalyticsService>(_ => MessageAnalyticsService.Instance);
        services.AddSingleton<IWebViewScriptGateway>(_ => WebViewScriptGateway.Instance);
        services.AddSingleton<IGitHubUpdateService>(_ => GitHubUpdateService.Instance);
        services.AddSingleton<IWebViewProfileManager>(_ => WebViewProfileManager.Instance);
        services.AddSingleton<ISystemTrayService>(_ => SystemTrayService.Instance);
        services.AddSingleton<IAppNotificationService>(_ => AppNotificationService.Instance);
        services.AddSingleton<ITaskbarBadgeService>(_ => TaskbarBadgeService.Instance);
        services.AddSingleton<IAiInferenceClient>(_ => OllamaInferenceClient.Instance);

        // Concrete services not yet behind interfaces.
        services.AddSingleton(_ => AdapterHealthMonitor.Instance);
        services.AddSingleton(_ => InstanceConnectionStatusService.Instance);
        services.AddSingleton(_ => OversightService.Instance);
        services.AddSingleton(_ => UnifiedMessengerDashboardService.Instance);
        services.AddSingleton(_ => PersonalDashboardService.Instance);
        services.AddSingleton(_ => DashboardRefreshCoordinator.Instance);
        services.AddSingleton(_ => UnifiedMessengerStateSyncService.Instance);
        services.AddSingleton(_ => ThreadDisplayOrderService.Instance);
        services.AddSingleton(_ => TriagePersistenceService.Instance);
        services.AddSingleton(_ => InstanceWebViewRegistry.Instance);
        services.AddSingleton(_ => ResourceMonitorService.Instance);
        services.AddSingleton(_ => WhatsAppBusinessContextService.Instance);
        services.AddSingleton(_ => OllamaRuntimeService.Instance);
        services.AddSingleton(_ => AiInferenceQueue.Instance);

        // Composition root — resolved last; its constructor receives IServiceProvider.
        services.AddSingleton<ApplicationServices>();

        return services;
    }
}
