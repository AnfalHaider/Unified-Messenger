using Microsoft.UI.Xaml;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Services;

/// <summary>
/// Composition root for shell-scoped services (core product only).
/// </summary>
public sealed partial class ApplicationServices
{
    public static ApplicationServices CreateDefault() => new();

    public ApplicationServices(
        IInstanceRegistryService? registry = null,
        IInstanceSessionManager? sessionManager = null,
        INavigationService? navigation = null,
        INotificationHubService? notificationHub = null,
        IAppSettingsService? appSettings = null,
        IThreadRegistryService? threadRegistry = null,
        IMessageTriageService? messageTriage = null,
        IMessageAnalyticsService? messageAnalytics = null,
        IWebViewScriptGateway? webViewScriptGateway = null,
        IGitHubUpdateService? gitHubUpdate = null,
        IDialogService? dialog = null,
        IWebViewProfileManager? webViewProfileManager = null,
        ISystemTrayService? systemTray = null,
        IAppNotificationService? appNotification = null,
        ITaskbarBadgeService? taskbarBadge = null,
        AdapterHealthMonitor? adapterHealth = null,
        InstanceConnectionStatusService? connectionStatus = null,
        OperationsCommandCenterService? operationsCommandCenter = null,
        UnifiedMessengerDashboardService? dashboard = null,
        PersonalDashboardService? personalDashboard = null,
        DashboardRefreshCoordinator? dashboardRefresh = null,
        UnifiedMessengerStateSyncService? stateSync = null,
        ThreadDisplayOrderService? threadDisplayOrder = null,
        TriagePersistenceService? triagePersistence = null,
        InstanceWebViewRegistry? webViewRegistry = null,
        ResourceMonitorService? resourceMonitor = null,
        WhatsAppBusinessContextService? whatsAppBusinessContext = null,
        OccQueueFilterState? occQueueFilter = null,
        OccFilterState? occFilter = null,
        OccDateRangeFilterState? occDateRangeFilter = null,
        OccViewModeState? occViewMode = null,
        IAiInferenceClient? aiInferenceClient = null,
        OllamaRuntimeService? ollamaRuntime = null,
        AiInferenceQueue? aiInferenceQueue = null)
    {
        Registry = registry ?? new InstanceRegistryService();
        SessionManager = sessionManager ?? InstanceSessionManager.Instance;
        Navigation = navigation ?? ShellNavigationService.Instance;
        NotificationHub = notificationHub ?? global::UnifiedMessenger.Services.NotificationHub.Instance;
        AppSettings = appSettings ?? AppSettingsService.Instance;
        ThreadRegistry = threadRegistry ?? ThreadRegistryService.Instance;
        MessageTriage = messageTriage ?? MessageTriageService.Instance;
        MessageAnalytics = messageAnalytics ?? MessageAnalyticsService.Instance;
        WebViewScriptGateway = webViewScriptGateway ?? global::UnifiedMessenger.Services.WebViewScriptGateway.Instance;
        GitHubUpdate = gitHubUpdate ?? GitHubUpdateService.Instance;
        Dialog = dialog ?? new WinUiDialogService();
        WebViewProfileManager = webViewProfileManager ?? global::UnifiedMessenger.Services.WebViewProfileManager.Instance;
        SystemTray = systemTray ?? SystemTrayService.Instance;
        AppNotification = appNotification ?? AppNotificationService.Instance;
        TaskbarBadge = taskbarBadge ?? TaskbarBadgeService.Instance;
        AdapterHealth = adapterHealth ?? AdapterHealthMonitor.Instance;
        ConnectionStatus = connectionStatus ?? InstanceConnectionStatusService.Instance;
        OperationsCommandCenter = operationsCommandCenter ?? OperationsCommandCenterService.Instance;
        Dashboard = dashboard ?? UnifiedMessengerDashboardService.Instance;
        PersonalDashboard = personalDashboard ?? PersonalDashboardService.Instance;
        DashboardRefresh = dashboardRefresh ?? DashboardRefreshCoordinator.Instance;
        StateSync = stateSync ?? UnifiedMessengerStateSyncService.Instance;
        ThreadDisplayOrder = threadDisplayOrder ?? ThreadDisplayOrderService.Instance;
        TriagePersistence = triagePersistence ?? TriagePersistenceService.Instance;
        WebViewRegistry = webViewRegistry ?? InstanceWebViewRegistry.Instance;
        ResourceMonitor = resourceMonitor ?? ResourceMonitorService.Instance;
        WhatsAppBusinessContext = whatsAppBusinessContext ?? WhatsAppBusinessContextService.Instance;
        OccFilter = occFilter ?? OccFilterState.Instance;
        OccQueueFilter = occQueueFilter ?? OccQueueFilterState.Instance;
        OccDateRangeFilter = occDateRangeFilter ?? OccDateRangeFilterState.Instance;
        OccViewMode = occViewMode ?? OccViewModeState.Instance;
        AiInferenceClient = aiInferenceClient ?? OllamaInferenceClient.Instance;
        OllamaRuntime = ollamaRuntime ?? OllamaRuntimeService.Instance;
        AiInferenceQueue = aiInferenceQueue ?? AiInferenceQueue.Instance;
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
