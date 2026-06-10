using System.Runtime.CompilerServices;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Blocks unexpected navigation before WebView2 leaves the allowed messaging origin surface.
/// </summary>
public static class WebViewNavigationGuard
{
    private static readonly string[] CommonOAuthHosts =
    [
        "accounts.google.com",
        "facebook.com",
        "www.facebook.com",
        "login.microsoftonline.com",
        "discordapp.com",
        "discord.gg",
        "slack.com"
    ];

    private static readonly HashSet<string> DefaultAllowedHosts = BuildDefaultAllowedHosts();

    private static readonly ConditionalWeakTable<CoreWebView2, HashSet<string>> WebViewAllowlists = new();

    public static void Attach(CoreWebView2 coreWebView) => Attach(coreWebView, additionalHosts: null);

    public static void Attach(CoreWebView2 coreWebView, IEnumerable<string>? additionalHosts)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);

        var allowlist = CreateAllowlist(additionalHosts);
        WebViewAllowlists.AddOrUpdate(coreWebView, allowlist);
        coreWebView.NavigationStarting -= OnNavigationStarting;
        coreWebView.NavigationStarting += OnNavigationStarting;
    }

    public static bool IsAllowedNavigationUri(string? uri, IEnumerable<string>? additionalHosts = null) =>
        IsAllowedNavigationUri(uri, CreateAllowlist(additionalHosts));

    internal static IEnumerable<string>? ExtractAdditionalHostsFromStartUrl(string? startUrl)
    {
        if (string.IsNullOrWhiteSpace(startUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(startUrl.Trim(), UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        return [uri.Host];
    }

    private static HashSet<string> BuildDefaultAllowedHosts()
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var platform in PlatformDefinition.All)
        {
            if (string.IsNullOrWhiteSpace(platform.DefaultUrl))
            {
                continue;
            }

            if (Uri.TryCreate(platform.DefaultUrl, UriKind.Absolute, out var uri) &&
                !string.IsNullOrWhiteSpace(uri.Host))
            {
                hosts.Add(uri.Host);
            }
        }

        foreach (var host in CommonOAuthHosts)
        {
            hosts.Add(host);
        }

        return hosts;
    }

    private static HashSet<string> CreateAllowlist(IEnumerable<string>? additionalHosts)
    {
        var allowlist = new HashSet<string>(DefaultAllowedHosts, StringComparer.OrdinalIgnoreCase);

        if (additionalHosts is null)
        {
            return allowlist;
        }

        foreach (var host in additionalHosts)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                continue;
            }

            allowlist.Add(host.Trim());
        }

        return allowlist;
    }

    private static bool IsHostAllowed(string host, IReadOnlySet<string> allowlist)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        foreach (var allowed in allowlist)
        {
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowedNavigationUri(string? uri, IReadOnlySet<string> allowlist)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsHostAllowed(parsed.Host, allowlist);
    }

    private static void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (sender is CoreWebView2 coreWebView &&
            WebViewAllowlists.TryGetValue(coreWebView, out var allowlist))
        {
            if (!IsAllowedNavigationUri(args.Uri, allowlist))
            {
                args.Cancel = true;
                System.Diagnostics.Debug.WriteLine($"Blocked WebView navigation to disallowed URI: {args.Uri}");
            }

            return;
        }

        if (!IsAllowedNavigationUri(args.Uri, DefaultAllowedHosts))
        {
            args.Cancel = true;
            System.Diagnostics.Debug.WriteLine($"Blocked WebView navigation to disallowed URI: {args.Uri}");
        }
    }
}
