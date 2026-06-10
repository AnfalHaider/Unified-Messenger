using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace UnifiedMessenger.Services;

public interface IWebViewProfileManager
{
    string UserDataFolder { get; }

    CoreWebView2Environment? SharedEnvironment { get; }

    Task<CoreWebView2Environment> EnsureEnvironmentAsync(CancellationToken cancellationToken = default);

    Task<WebView2> CreateWebViewAsync(
        string profileName,
        string? startUrl = null,
        CancellationToken cancellationToken = default);

    Task PermanentlyDeleteProfileAsync(
        string profileName,
        WebView2? activeWebView = null,
        CancellationToken cancellationToken = default);
}
