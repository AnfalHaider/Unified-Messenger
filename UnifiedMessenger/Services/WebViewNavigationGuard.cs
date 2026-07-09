using System.Runtime.CompilerServices;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Blocks unexpected navigation before WebView2 leaves the allowed messaging origin surface.
/// </summary>
public static class WebViewNavigationGuard
{
    /// <summary>
    /// Sentinel host that, when present in an allowlist, permits navigation to ANY http/https host (non-web
    /// schemes stay blocked). Used for Custom-URL / generic tabs — the user chose to monitor an arbitrary
    /// website, so its own redirects/links (OAuth, CDNs, cross-domain hops) must not be cancelled.
    /// </summary>
    internal const string AllowAllHostsSentinel = "*";

    public static void AttachAllowingAllHosts(CoreWebView2 coreWebView) =>
        Attach(coreWebView, [AllowAllHostsSentinel]);

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

    // Second-level domains (e.g. example.CO.UK) so RegistrableDomain doesn't over-broaden a custom URL to the
    // whole "co.uk". MUST be declared before DefaultAllowedHosts — its initializer calls RegistrableDomain.
    private static readonly HashSet<string> SecondLevelTlds =
        new(StringComparer.OrdinalIgnoreCase) { "co", "com", "org", "net", "gov", "edu", "ac" };

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
        coreWebView.NewWindowRequested -= OnNewWindowRequested;
        coreWebView.NewWindowRequested += OnNewWindowRequested;
    }

    public static void Detach(CoreWebView2 coreWebView)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);
        coreWebView.NavigationStarting -= OnNavigationStarting;
        coreWebView.NewWindowRequested -= OnNewWindowRequested;
        WebViewAllowlists.Remove(coreWebView);
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

        // Allow the whole registrable domain of the start URL — a site's own redirects/links commonly hop
        // across its subdomains, and a monitored page should be navigable within its own domain.
        return [uri.Host, RegistrableDomain(uri.Host)];
    }

    /// <summary>The registrable domain (eTLD+1) of a host — e.g. business.google.com → google.com. Suffix
    /// matching in <see cref="IsHostAllowed"/> then covers every subdomain.</summary>
    internal static string RegistrableDomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return host;
        }

        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length <= 2)
        {
            return host;
        }

        // e.g. www.example.co.uk → example.co.uk ; business.google.com → google.com
        return labels.Length >= 3 && SecondLevelTlds.Contains(labels[^2])
            ? string.Join('.', labels[^3], labels[^2], labels[^1])
            : string.Join('.', labels[^2], labels[^1]);
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
                // Allow the whole registrable domain so a platform's cross-subdomain redirects aren't blocked
                // (business.google.com → www.google.com onboarding / sign-in hops were the Google Business bug).
                hosts.Add(RegistrableDomain(uri.Host));
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

        // Custom-URL / generic tabs are attached with this sentinel — allow any host (scheme is still checked
        // by the caller, so only http/https gets here).
        if (allowlist.Contains(AllowAllHostsSentinel))
        {
            return true;
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

        // Allow http as well as https. Most sites are https, but a custom URL may be entered as http:// or
        // first hop through an http endpoint; blocking it left the tab stuck on about:blank.
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
                AppLogger.LogWarning("WebView.Nav", $"Blocked navigation to disallowed URI: {args.Uri}");
            }

            return;
        }

        if (!IsAllowedNavigationUri(args.Uri, DefaultAllowedHosts))
        {
            args.Cancel = true;
            AppLogger.LogWarning("WebView.Nav", $"Blocked navigation to disallowed URI: {args.Uri}");
        }
    }

    private static void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        // Always suppress popup windows. If the target URL is in the allow-list, navigate the
        // current WebView frame instead; otherwise discard silently.
        args.Handled = true;

        if (sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        WebViewAllowlists.TryGetValue(coreWebView, out var allowlist);
        var effectiveAllowlist = (IReadOnlySet<string>?)allowlist ?? DefaultAllowedHosts;

        if (IsAllowedNavigationUri(args.Uri, effectiveAllowlist))
        {
            coreWebView.Navigate(args.Uri);
        }
        else
        {
            AppLogger.LogWarning("WebView.Nav", $"Blocked new-window request to disallowed URI: {args.Uri}");
        }
    }
}
