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

    // Bookkeeping only, so a re-Attach can unsubscribe the previous handlers. The navigation decision must
    // NEVER depend on a lookup here: CoreWebView2 is a CsWinRT projection, so the managed wrapper we key on
    // can be collected and re-created for the same native object, which silently drops the entry. That used
    // to fail back to DefaultAllowedHosts — invisible for built-in platforms (their hosts are in the
    // defaults) but it cancelled every Custom-URL tab's navigation and left it on about:blank.
    // Each binding therefore captures its own allowlist in the handler closure.
    private static readonly ConditionalWeakTable<CoreWebView2, GuardBinding> Bindings = new();

    public static void Attach(CoreWebView2 coreWebView) => Attach(coreWebView, additionalHosts: null);

    public static void Attach(CoreWebView2 coreWebView, IEnumerable<string>? additionalHosts)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);

        if (Bindings.TryGetValue(coreWebView, out var previous))
        {
            previous.Detach();
            Bindings.Remove(coreWebView);
        }

        var allowlist = CreateAllowlist(additionalHosts);
        var binding = new GuardBinding(allowlist);
        binding.Attach(coreWebView);
        Bindings.Add(coreWebView, binding);

        AppLogger.LogInfo(
            "WebView.Nav",
            $"Navigation guard attached: allowAllHosts={allowlist.Contains(AllowAllHostsSentinel)} hosts={allowlist.Count}");
    }

    public static void Detach(CoreWebView2 coreWebView)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);

        if (Bindings.TryGetValue(coreWebView, out var binding))
        {
            binding.Detach();
            Bindings.Remove(coreWebView);
        }
    }

    /// <summary>
    /// One WebView's guard subscription. Holds the allowlist and its own handler delegates so the decision
    /// path never needs a table lookup, and so Detach can unsubscribe exactly what Attach subscribed.
    /// </summary>
    private sealed class GuardBinding(HashSet<string> allowlist)
    {
        private Action? _unsubscribe;

        internal void Attach(CoreWebView2 coreWebView)
        {
            void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args) =>
                HandleNavigationStarting(args, allowlist);

            void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args) =>
                HandleNewWindowRequested(sender, args, allowlist);

            coreWebView.NavigationStarting += OnNavigationStarting;
            coreWebView.NewWindowRequested += OnNewWindowRequested;

            _unsubscribe = () =>
            {
                coreWebView.NavigationStarting -= OnNavigationStarting;
                coreWebView.NewWindowRequested -= OnNewWindowRequested;
            };
        }

        internal void Detach()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
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

    // In-page downloads and media resolve to blob:/data: URIs — e.g. WhatsApp decrypts a received file to a
    // blob before saving it. These are not cross-origin navigations; blocking them kills the download.
    // Every WebView2 starts on about:blank, so it shows up as a navigation. Cancelling it changes nothing
    // (the page is already blank) but it logged a "Blocked navigation" warning on every session start.
    private static bool IsBlankPage(string? uri) =>
        string.IsNullOrWhiteSpace(uri) || uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase);

    private static bool IsDownloadLikeScheme(string? uri) =>
        !string.IsNullOrWhiteSpace(uri) &&
        (uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
         uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase));

    private static void HandleNavigationStarting(
        CoreWebView2NavigationStartingEventArgs args,
        IReadOnlySet<string> allowlist)
    {
        if (IsDownloadLikeScheme(args.Uri) || IsBlankPage(args.Uri))
        {
            return;
        }

        if (!IsAllowedNavigationUri(args.Uri, allowlist))
        {
            args.Cancel = true;
            AppLogger.LogWarning("WebView.Nav", $"Blocked navigation to disallowed URI: {args.Uri}");
        }
    }

    private static void HandleNewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs args,
        IReadOnlySet<string> allowlist)
    {
        // Always suppress popup windows. If the target URL is in the allow-list, navigate the
        // current WebView frame instead; otherwise discard silently.
        args.Handled = true;

        if (sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        if (IsAllowedNavigationUri(args.Uri, allowlist))
        {
            coreWebView.Navigate(args.Uri);
        }
        else
        {
            AppLogger.LogWarning("WebView.Nav", $"Blocked new-window request to disallowed URI: {args.Uri}");
        }
    }
}
