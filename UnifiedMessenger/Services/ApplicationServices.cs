using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Services;

/// <summary>
/// Composition root for shell-scoped services (core product only).
/// Constructed by the DI container built in <see cref="ServiceRegistration.AddApplicationServices"/>.
/// </summary>
public sealed partial class ApplicationServices
{
    public static ApplicationServices CreateDefault()
    {
        var provider = new ServiceCollection()
            .AddApplicationServices()
            .BuildServiceProvider();
        return provider.GetRequiredService<ApplicationServices>();
    }

    public ApplicationServices(IServiceProvider provider)
    {
        Registry = provider.GetRequiredService<IInstanceRegistryService>();
        SessionManager = provider.GetRequiredService<IInstanceSessionManager>();
        Navigation = provider.GetRequiredService<INavigationService>();
        NotificationHub = provider.GetRequiredService<INotificationHubService>();
        AppSettings = provider.GetRequiredService<IAppSettingsService>();
        ThreadRegistry = provider.GetRequiredService<IThreadRegistryService>();
        MessageTriage = provider.GetRequiredService<IMessageTriageService>();
        MessageAnalytics = provider.GetRequiredService<IMessageAnalyticsService>();
        WebViewScriptGateway = provider.GetRequiredService<IWebViewScriptGateway>();
        GitHubUpdate = provider.GetRequiredService<IGitHubUpdateService>();
        Dialog = provider.GetRequiredService<IDialogService>();
        WebViewProfileManager = provider.GetRequiredService<IWebViewProfileManager>();
        SystemTray = provider.GetRequiredService<ISystemTrayService>();
        AppNotification = provider.GetRequiredService<IAppNotificationService>();
        TaskbarBadge = provider.GetRequiredService<ITaskbarBadgeService>();
        AdapterHealth = provider.GetRequiredService<AdapterHealthMonitor>();
        ConnectionStatus = provider.GetRequiredService<InstanceConnectionStatusService>();
        OperationsCommandCenter = provider.GetRequiredService<OperationsCommandCenterService>();
        Oversight = provider.GetRequiredService<OversightService>();
        Dashboard = provider.GetRequiredService<UnifiedMessengerDashboardService>();
        PersonalDashboard = provider.GetRequiredService<PersonalDashboardService>();
        DashboardRefresh = provider.GetRequiredService<DashboardRefreshCoordinator>();
        StateSync = provider.GetRequiredService<UnifiedMessengerStateSyncService>();
        ThreadDisplayOrder = provider.GetRequiredService<ThreadDisplayOrderService>();
        TriagePersistence = provider.GetRequiredService<TriagePersistenceService>();
        WebViewRegistry = provider.GetRequiredService<InstanceWebViewRegistry>();
        ResourceMonitor = provider.GetRequiredService<ResourceMonitorService>();
        WhatsAppBusinessContext = provider.GetRequiredService<WhatsAppBusinessContextService>();
        OccQueueFilter = provider.GetRequiredService<OccQueueFilterState>();
        OccFilter = provider.GetRequiredService<OccFilterState>();
        OccDateRangeFilter = provider.GetRequiredService<OccDateRangeFilterState>();
        OccViewMode = provider.GetRequiredService<OccViewModeState>();
        AiInferenceClient = provider.GetRequiredService<IAiInferenceClient>();
        OllamaRuntime = provider.GetRequiredService<OllamaRuntimeService>();
        AiInferenceQueue = provider.GetRequiredService<AiInferenceQueue>();
    }

    public IInstanceRegistryService Registry { get; }

    public IInstanceSessionManager SessionManager { get; }

    public INavigationService Navigation { get; }

    public INotificationHubService NotificationHub { get; }

    public IAppSettingsService AppSettings { get; }

    public IThreadRegistryService ThreadRegistry { get; }

    public IMessageTriageService MessageTriage { get; }

    public IMessageAnalyticsService MessageAnalytics { get; }

    public IWebViewScriptGateway WebViewScriptGateway { get; }

    public IGitHubUpdateService GitHubUpdate { get; }

    public IDialogService Dialog { get; }

    public IWebViewProfileManager WebViewProfileManager { get; }

    public ISystemTrayService SystemTray { get; }

    public IAppNotificationService AppNotification { get; }

    public ITaskbarBadgeService TaskbarBadge { get; }

    public AdapterHealthMonitor AdapterHealth { get; }

    public InstanceConnectionStatusService ConnectionStatus { get; }

    public OperationsCommandCenterService OperationsCommandCenter { get; }

    public OversightService Oversight { get; }

    public UnifiedMessengerDashboardService Dashboard { get; }

    public PersonalDashboardService PersonalDashboard { get; }

    public DashboardRefreshCoordinator DashboardRefresh { get; }

    public UnifiedMessengerStateSyncService StateSync { get; }

    public ThreadDisplayOrderService ThreadDisplayOrder { get; }

    public TriagePersistenceService TriagePersistence { get; }

    public InstanceWebViewRegistry WebViewRegistry { get; }

    public ResourceMonitorService ResourceMonitor { get; }

    public WhatsAppBusinessContextService WhatsAppBusinessContext { get; }

    public OccFilterState OccFilter { get; }

    public OccQueueFilterState OccQueueFilter { get; }

    public OccDateRangeFilterState OccDateRangeFilter { get; }

    public OccViewModeState OccViewMode { get; }

    public IAiInferenceClient AiInferenceClient { get; }

    public OllamaRuntimeService OllamaRuntime { get; }

    public AiInferenceQueue AiInferenceQueue { get; }

    public void ConfigureUi(XamlRoot? xamlRoot) =>
        ConfigureUi(() => xamlRoot!);

    public void ConfigureUi(Func<XamlRoot> xamlRootProvider)
    {
        ArgumentNullException.ThrowIfNull(xamlRootProvider);

        if (Dialog is WinUiDialogService winUiDialog)
        {
            winUiDialog.SetXamlRootProvider(xamlRootProvider);
        }
    }
}
