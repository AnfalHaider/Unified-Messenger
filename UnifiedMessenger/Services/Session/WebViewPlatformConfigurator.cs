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

        // Received files (WhatsApp media and documents, exports) are saved where the OWNER chooses. The
        // previous handler left Handled = false, which gave WebView2's built-in flyout and dropped every
        // file into a folder the owner never picked and — for an unpackaged host — cannot easily find.
        // DownloadLocationPrompt shows the system save dialog instead and owns the deferral that the
        // asynchronous picker requires. Idempotent: Apply can run more than once.
        DownloadLocationPrompt.Attach(coreWebView);

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


    private static void OnDiscordNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        if (sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        // Carried the same two defects as the general handler and had to be fixed alongside it, or Discord
        // would keep swallowing downloads and links while every other channel worked.
        if (WebViewNavigationGuard.IsDownloadLikeUri(args.Uri))
        {
            args.Handled = false; // hand it back to WebView2's download pipeline
            return;
        }

        args.Handled = true;

        if (WebViewNavigationGuard.IsAllowedNavigationUri(args.Uri))
        {
            coreWebView.Navigate(args.Uri);
            return;
        }

        WebViewNavigationGuard.TryOpenExternally(args.Uri, args.IsUserInitiated);
    }
}
