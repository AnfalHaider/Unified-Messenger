using Microsoft.Web.WebView2.Core;

namespace UnifiedMessenger.Services;

/// <summary>
/// Blocks unexpected navigation schemes before WebView2 leaves the messaging origin surface.
/// </summary>
public static class WebViewNavigationGuard
{
    public static void Attach(CoreWebView2 coreWebView)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);
        coreWebView.NavigationStarting += OnNavigationStarting;
    }

    public static bool IsAllowedNavigationUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        return parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
               parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!IsAllowedNavigationUri(args.Uri))
        {
            args.Cancel = true;
            System.Diagnostics.Debug.WriteLine($"Blocked WebView navigation to disallowed URI: {args.Uri}");
        }
    }
}
