using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Applies per-platform WebView2 settings so embedded clients (e.g. Discord) do not block login.
/// </summary>
public static class WebViewPlatformConfigurator
{
    internal const string ChromeDesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private static readonly HashSet<string> DiscordNavigationHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord.com",
        "discordapp.com",
        "discord.gg",
        "discord.new",
        "discord.media",
        "discord.co"
    };

    public static void Apply(CoreWebView2 coreWebView, string? platformId)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);

        var settings = coreWebView.Settings;
        settings.IsWebMessageEnabled = true;
        settings.AreDefaultScriptDialogsEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = true;

        // Let received files (WhatsApp media/documents, exports) download with WebView2's built-in,
        // browser-style experience (progress flyout + Save-As + Downloads folder). Without this the
        // nav guard's blanket blocking leaves downloads dead. Idempotent — Apply can run more than once.
        coreWebView.DownloadStarting -= OnDownloadStarting;
        coreWebView.DownloadStarting += OnDownloadStarting;

        var normalized = PlatformDefinition.NormalizePlatformId(platformId);

        // WhatsApp Web is tuned for WebView2's default UA (and the scraper depends on it) — leave it alone.
        // Every embed channel (Google Business, Meta, Messenger, Telegram, Discord, Instagram, generic) gets
        // a clean desktop Chrome UA: Google/Meta reject WebView2's default UA with "browser not supported".
        var isWhatsApp = normalized is "whatsapp" or "whatsappbusiness";
        if (!isWhatsApp)
        {
            settings.UserAgent = ChromeDesktopUserAgent;
        }

        // Discord opens auth/links in new windows; route them back into the same view so login completes.
        if (normalized.Equals("discord", StringComparison.OrdinalIgnoreCase))
        {
            coreWebView.NewWindowRequested -= OnDiscordNewWindowRequested;
            coreWebView.NewWindowRequested += OnDiscordNewWindowRequested;
        }
    }

    internal static bool IsDiscordNavigationHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        foreach (var allowed in DiscordNavigationHosts)
        {
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsValidDiscordStartUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http"))
        {
            return false;
        }

        return IsDiscordNavigationHost(uri.Host);
    }

    private static void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs args)
    {
        // Keep WebView2's default download UI — that IS the browser behaviour. We only need to not cancel;
        // leaving Handled = false shows the built-in progress flyout and lets the user pick the location.
        args.Handled = false;
    }

    private static void OnDiscordNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        if (sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        args.Handled = true;

        if (WebViewNavigationGuard.IsAllowedNavigationUri(args.Uri))
        {
            coreWebView.Navigate(args.Uri);
        }
    }
}
