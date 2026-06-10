using Microsoft.UI.Xaml;
using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.Services.Contracts;
using UnifiedMessenger.Services.Ollama;
using UnifiedMessenger.Services.PlatformModules;
using UnifiedMessenger.Services.VoiceNotes;

namespace UnifiedMessenger.Services;

/// <summary>
/// Composition root for shell-scoped services. Existing singletons remain until Waves 7–10 migrate consumers.
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
        IPlatformModuleRegistry? platformModules = null,
        IRichTriageStoreService? richTriageStore = null,
        IWebViewProfileManager? webViewProfileManager = null,
        ISystemTrayService? systemTray = null,
        IGlobalHotkeyService? globalHotkey = null,
        IAppNotificationService? appNotification = null,
        ITaskbarBadgeService? taskbarBadge = null,
        IOllamaOrchestrationService? ollama = null,
        AdapterHealthMonitor? adapterHealth = null,
        InstanceConnectionStatusService? connectionStatus = null,
        AutoDraftOrchestrator? autoDraft = null,
        HotkeyCopilotOrchestrator? hotkeyCopilot = null,
        BranchPulseService? branchPulse = null,
        OperationsCommandCenterService? operationsCommandCenter = null,
        UnifiedMessengerDashboardService? dashboard = null,
        PersonalDashboardService? personalDashboard = null,
        ProfessionalWorkspaceService? professionalWorkspace = null,
        DashboardRefreshCoordinator? dashboardRefresh = null,
        DashboardScrapeOrchestrator? dashboardScrape = null,
        DashboardScrapeStatusService? dashboardScrapeStatus = null,
        UnifiedMessengerInsightsEngine? insightsEngine = null,
        UnifiedMessengerStateSyncService? stateSync = null,
        ThreadDisplayOrderService? threadDisplayOrder = null,
        InstanceWebViewRegistry? webViewRegistry = null,
        ResourceMonitorService? resourceMonitor = null,
        VoiceNotePipelineService? voiceNotePipeline = null,
        WhatsAppBusinessContextService? whatsAppBusinessContext = null,
        OllamaInferenceCoordinator? ollamaInference = null,
        BackfillSyncManager? backfill = null,
        OccFilterState? occFilter = null,
        OccSnapshotExportService? occSnapshotExport = null)
    {
        Registry = registry ?? new InstanceRegistryService();
        PlatformModules = platformModules ?? PlatformModuleRegistry.Instance;
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
        RichTriageStore = richTriageStore ?? RichTriageStoreService.Instance;
        WebViewProfileManager = webViewProfileManager ?? global::UnifiedMessenger.Services.WebViewProfileManager.Instance;
        SystemTray = systemTray ?? SystemTrayService.Instance;
        GlobalHotkey = globalHotkey ?? GlobalHotkeyService.Instance;
        AppNotification = appNotification ?? AppNotificationService.Instance;
        TaskbarBadge = taskbarBadge ?? TaskbarBadgeService.Instance;
        Ollama = ollama ?? OllamaOrchestrationService.Instance;
        AdapterHealth = adapterHealth ?? AdapterHealthMonitor.Instance;
        ConnectionStatus = connectionStatus ?? InstanceConnectionStatusService.Instance;
        AutoDraft = autoDraft ?? AutoDraftOrchestrator.Instance;
        HotkeyCopilot = hotkeyCopilot ?? HotkeyCopilotOrchestrator.Instance;
        BranchPulse = branchPulse ?? BranchPulseService.Instance;
        OperationsCommandCenter = operationsCommandCenter ?? OperationsCommandCenterService.Instance;
        Dashboard = dashboard ?? UnifiedMessengerDashboardService.Instance;
        PersonalDashboard = personalDashboard ?? PersonalDashboardService.Instance;
        ProfessionalWorkspace = professionalWorkspace ?? ProfessionalWorkspaceService.Instance;
        DashboardRefresh = dashboardRefresh ?? DashboardRefreshCoordinator.Instance;
        DashboardScrape = dashboardScrape ?? DashboardScrapeOrchestrator.Instance;
        DashboardScrapeStatus = dashboardScrapeStatus ?? DashboardScrapeStatusService.Instance;
        InsightsEngine = insightsEngine ?? UnifiedMessengerInsightsEngine.Instance;
        StateSync = stateSync ?? UnifiedMessengerStateSyncService.Instance;
        ThreadDisplayOrder = threadDisplayOrder ?? ThreadDisplayOrderService.Instance;
        WebViewRegistry = webViewRegistry ?? InstanceWebViewRegistry.Instance;
        ResourceMonitor = resourceMonitor ?? ResourceMonitorService.Instance;
        VoiceNotePipeline = voiceNotePipeline ?? VoiceNotePipelineService.Instance;
        WhatsAppBusinessContext = whatsAppBusinessContext ?? WhatsAppBusinessContextService.Instance;
        OllamaInference = ollamaInference ?? OllamaInferenceCoordinator.Instance;
        Backfill = backfill ?? BackfillSyncManager.Instance;
        OccFilter = occFilter ?? OccFilterState.Instance;
        OccSnapshotExport = occSnapshotExport ?? OccSnapshotExportService.Instance;
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

    public IPlatformModuleRegistry PlatformModules { get; }

    public IRichTriageStoreService RichTriageStore { get; }

    public IWebViewProfileManager WebViewProfileManager { get; }

    public ISystemTrayService SystemTray { get; }

    public IGlobalHotkeyService GlobalHotkey { get; }

    public IAppNotificationService AppNotification { get; }

    public ITaskbarBadgeService TaskbarBadge { get; }

    public IOllamaOrchestrationService Ollama { get; }

    public AdapterHealthMonitor AdapterHealth { get; }

    public InstanceConnectionStatusService ConnectionStatus { get; }

    public AutoDraftOrchestrator AutoDraft { get; }

    public HotkeyCopilotOrchestrator HotkeyCopilot { get; }

    public BranchPulseService BranchPulse { get; }

    public OperationsCommandCenterService OperationsCommandCenter { get; }

    public UnifiedMessengerDashboardService Dashboard { get; }

    public PersonalDashboardService PersonalDashboard { get; }

    public ProfessionalWorkspaceService ProfessionalWorkspace { get; }

    public DashboardRefreshCoordinator DashboardRefresh { get; }

    public DashboardScrapeOrchestrator DashboardScrape { get; }

    public DashboardScrapeStatusService DashboardScrapeStatus { get; }

    public UnifiedMessengerInsightsEngine InsightsEngine { get; }

    public UnifiedMessengerStateSyncService StateSync { get; }

    public ThreadDisplayOrderService ThreadDisplayOrder { get; }

    public InstanceWebViewRegistry WebViewRegistry { get; }

    public ResourceMonitorService ResourceMonitor { get; }

    public VoiceNotePipelineService VoiceNotePipeline { get; }

    public WhatsAppBusinessContextService WhatsAppBusinessContext { get; }

    public OllamaInferenceCoordinator OllamaInference { get; }

    public BackfillSyncManager Backfill { get; }

    public OccFilterState OccFilter { get; }

    public OccSnapshotExportService OccSnapshotExport { get; }

    public void ConfigureUi(XamlRoot? xamlRoot) =>
        ConfigureUi(() => xamlRoot!);

    /// <summary>
    /// Binds dialog UI to a XamlRoot resolved when dialogs are shown (safe during window construction).
    /// </summary>
    public void ConfigureUi(Func<XamlRoot> xamlRootProvider)
    {
        ArgumentNullException.ThrowIfNull(xamlRootProvider);

        if (Dialog is WinUiDialogService winUiDialog)
        {
            winUiDialog.SetXamlRootProvider(xamlRootProvider);
        }
    }
}
