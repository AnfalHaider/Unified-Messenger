using Microsoft.UI.Xaml;
using UnifiedMessenger.Services.Contracts;
using UnifiedMessenger.Services.PlatformModules;

namespace UnifiedMessenger.Services;

/// <summary>
/// Composition root for shell-scoped services. Existing singletons remain until Waves 7–10 migrate consumers.
/// </summary>
public sealed class ApplicationServices
{
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
        IPlatformModuleRegistry? platformModules = null)
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
