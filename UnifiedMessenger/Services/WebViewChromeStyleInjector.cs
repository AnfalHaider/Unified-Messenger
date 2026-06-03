using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UnifiedMessenger.Services;

public static class WebViewChromeStyleInjector
{
    public static async Task InjectAsync(
        CoreWebView2 coreWebView,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        var css = await LoadChromeCssAsync(platformId, cancellationToken);
        if (string.IsNullOrWhiteSpace(css))
        {
            return;
        }

        var cssLiteral = JsonSerializer.Serialize(css);
        var script = $$"""
            (function () {
              if (window.__unifiedMessengerChromeInjected) { return; }
              window.__unifiedMessengerChromeInjected = true;
              var style = document.createElement('style');
              style.id = 'unified-messenger-chrome';
              style.textContent = {{cssLiteral}};
              (document.head || document.documentElement).appendChild(style);
            })();
            """;

        await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private static async Task<string> LoadChromeCssAsync(string platformId, CancellationToken cancellationToken)
    {
        var fileName = platformId.ToLowerInvariant() switch
        {
            "whatsapp" => "whatsapp-chrome.css",
            "telegram" => "telegram-chrome.css",
            "messenger" => "messenger-chrome.css",
            _ => "generic-chrome.css"
        };

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Styles", fileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "Assets", "Styles", "generic-chrome.css");
        }

        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : string.Empty;
    }
}
