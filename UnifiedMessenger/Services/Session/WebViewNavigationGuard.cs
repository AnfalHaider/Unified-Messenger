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
    //
    // A dropped entry here leaks a subscription rather than changing a decision, and every Detach call
    // site closes the WebView immediately afterwards (InstanceSessionManager), so the handlers it leaves
    // behind are attached to a native object that is about to go away and can never fire again. Re-Attach
    // only happens at WebView creation, on a fresh object. So this one is left keyed as it is — the
    // asymmetry that made PlatformNavigationHooks worth re-keying does not apply.
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
                HandleNewWindowRequested(sender, args);

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

    /// <summary>
    /// True for the in-page schemes a download resolves to. Public so the per-platform configurators apply
    /// the same rule — the Discord handler had its own copy of this logic without the exemption, so
    /// downloads worked on every channel except that one.
    /// </summary>
    public static bool IsDownloadLikeUri(string? uri) => IsDownloadLikeScheme(uri);

    private static bool IsDownloadLikeScheme(string? uri) =>
        !string.IsNullOrWhiteSpace(uri) &&
        (uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
         uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase));

    /// <summary>What to do with a <c>target="_blank"</c> / <c>window.open</c> request.</summary>
    internal enum NewWindowAction
    {
        /// <summary>A download in disguise — hand it back to WebView2's own pipeline.</summary>
        LetWebViewHandle,

        /// <summary>Replace the current page. Only ever right when the session must be preserved.</summary>
        NavigateInFrame,

        /// <summary>The owner's own link — their browser.</summary>
        OpenExternally,

        /// <summary>Neither safe to shell out nor part of the site. Dropped, with a log line.</summary>
        Block
    }

    /// <summary>
    /// Routes a new-window request. Pure, so the decision can be asserted without a live
    /// <see cref="CoreWebView2"/> — which is why it was never covered before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The defect this replaces.</b> The rule used to be "allow-listed host → hop the current frame".
    /// The allowlist is built from every platform's <i>registrable domain</i> plus the OAuth hosts, so from
    /// WhatsApp Web it spans all of <c>whatsapp.com</c> AND <c>google.com</c>. Any help link, any marketing
    /// link, any <c>google.com</c> link a customer sent therefore replaced the scraped WhatsApp session in
    /// place — and back/forward are hidden for exactly the WhatsApp family, because
    /// <c>IsPlatformModuleEnabled</c> is true for them and <c>MainWindow</c> collapses the nav controls when
    /// it is. The owner was left on a marketing page with no way back, and oversight for that account
    /// stopped until they found right-click → Refresh WebView.
    /// </para>
    /// <para>
    /// <b>Why same-host and not same-site.</b> Same registrable domain would keep <c>faq.whatsapp.com</c>
    /// in-frame, which is the exact case that stranded the owner. This handler only sees deliberate
    /// new-window intents — a redirect is a navigation and goes through
    /// <see cref="HandleNavigationStarting"/> — so "the site opened its own page in a new tab" is the only
    /// case where replacing the frame is right.
    /// </para>
    /// <para>
    /// <b>Why OAuth hosts stay in-frame.</b> A sign-in popup handed to the default browser would put the
    /// session cookie in the owner's browser rather than this WebView2 profile, so the sign-in it was
    /// serving could never complete. Those hosts must keep the cookie jar.
    /// </para>
    /// </remarks>
    internal static NewWindowAction ResolveNewWindowAction(
        string? targetUri,
        string? currentPageUri,
        bool isUserInitiated)
    {
        if (IsDownloadLikeScheme(targetUri))
        {
            return NewWindowAction.LetWebViewHandle;
        }

        if (SharesHost(targetUri, currentPageUri) || IsOAuthHost(targetUri))
        {
            return NewWindowAction.NavigateInFrame;
        }

        if (isUserInitiated && IsExternallyOpenableUri(targetUri))
        {
            return NewWindowAction.OpenExternally;
        }

        return NewWindowAction.Block;
    }

    private static bool SharesHost(string? a, string? b) =>
        TryHost(a, out var hostA) && TryHost(b, out var hostB) &&
        string.Equals(hostA, hostB, StringComparison.OrdinalIgnoreCase);

    private static bool IsOAuthHost(string? uri) =>
        TryHost(uri, out var host) &&
        CommonOAuthHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    private static bool TryHost(string? uri, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(uri) ||
            !Uri.TryCreate(uri.Trim(), UriKind.Absolute, out var parsed) ||
            string.IsNullOrWhiteSpace(parsed.Host))
        {
            return false;
        }

        host = parsed.Host;
        return true;
    }

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
            // Cancelling is still right — the monitored account must not be navigated away from, or the
            // owner loses their WhatsApp session to a link. But cancelling and stopping there was a dead
            // end: a link without target="_blank" simply did nothing on click.
            //
            // The cancel keeps the session; the launch honours the click. Both, not one.
            args.Cancel = true;

            if (TryOpenExternally(args.Uri, args.IsUserInitiated))
            {
                return;
            }

            AppLogger.LogWarning("WebView.Nav", $"Blocked navigation to disallowed URI: {args.Uri}");
        }
    }

    private static void HandleNewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs args)
    {
        // A DOWNLOAD, not a popup. WhatsApp decrypts a received file to a blob and opens it — which arrives
        // here as a new-window request for a `blob:` URI. This handler used to set Handled = true and then
        // discard anything that was not an allow-listed http(s) host, so every single download the owner
        // tried silently did nothing: no file, no error, no log line they would ever see.
        //
        // HandleNavigationStarting already had this exemption. This path never got it.
        //
        // Leaving Handled = false hands the request back to WebView2, which runs its own download pipeline
        // and shows the built-in save UI — the browser behaviour the owner expects.
        var coreWebView = sender as CoreWebView2;
        var action = ResolveNewWindowAction(args.Uri, coreWebView?.Source, args.IsUserInitiated);

        if (action == NewWindowAction.LetWebViewHandle)
        {
            args.Handled = false;
            return;
        }

        args.Handled = true;

        switch (action)
        {
            case NewWindowAction.NavigateInFrame when coreWebView is not null:
                coreWebView.Navigate(args.Uri);
                return;

            // Anything else the owner deliberately clicked is THEIR link, and belongs in their own browser.
            // Customers send links constantly; every one of them used to vanish on click.
            case NewWindowAction.OpenExternally when TryOpenExternally(args.Uri, args.IsUserInitiated):
                return;
        }

        AppLogger.LogWarning("WebView.Nav", $"Blocked new-window request to disallowed URI: {args.Uri}");
    }

    /// <summary>
    /// Schemes that may be handed to the operating system's default handler.
    /// </summary>
    /// <remarks>
    /// Deliberately a short allow-list rather than "anything not http". Shelling out an arbitrary scheme is
    /// how a malicious page gets local code run: <c>file:</c> opens Explorer at a path of its choosing,
    /// <c>javascript:</c> and <c>vbscript:</c> are script, and registered custom protocols (<c>ms-*</c>,
    /// installer handlers, remote-desktop) invoke other applications with attacker-controlled arguments.
    /// A page in a WebView2 is untrusted input, so only the four schemes a customer message legitimately
    /// contains are forwarded.
    /// </remarks>
    /// <summary>
    /// Whether this URI would be forwarded to the default browser. Exposed so the scheme boundary can be
    /// asserted without actually launching anything — the first version of those tests called the launcher
    /// and opened four windows on the machine running them.
    /// </summary>
    public static bool IsExternallyOpenableUri(string? uri) =>
        !string.IsNullOrWhiteSpace(uri) &&
        Uri.TryCreate(uri.Trim(), UriKind.Absolute, out var parsed) &&
        IsExternallyOpenableScheme(parsed);

    private static bool IsExternallyOpenableScheme(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals("tel", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens a link the owner clicked in the system's default browser. Returns false when the link is not
    /// something that should be forwarded, so the caller can fall through to blocking it.
    /// </summary>
    /// <param name="userInitiated">
    /// Only ever true for a real click. Automatic navigations, redirects and tracking hops must not be able
    /// to launch a browser window — that would turn any page in a monitored account into a pop-up cannon.
    /// </param>
    /// <remarks>
    /// This does not weaken the project's "zero oversight data leaves the machine" rule. That rule governs
    /// data the app <i>derives</i> — metrics, message text, customer identities. A link the owner clicked is
    /// the owner's own request, and handing it to their browser sends nothing the app produced.
    /// </remarks>
    internal static bool TryOpenExternally(string? uri, bool userInitiated)
    {
        if (!userInitiated || string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        if (!Uri.TryCreate(uri.Trim(), UriKind.Absolute, out var parsed) ||
            !IsExternallyOpenableScheme(parsed))
        {
            return false;
        }

        try
        {
            // UseShellExecute is what routes to the user's default handler rather than trying to execute
            // the URL as a program.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = parsed.AbsoluteUri,
                UseShellExecute = true
            });

            AppLogger.LogInfo("WebView.Nav", $"Opened externally in the default browser: {parsed.Host}");
            return true;
        }
        catch (Exception ex)
        {
            // A missing or misconfigured default browser must not take down the session. Log and let the
            // caller block, so the outcome is the old behaviour rather than a crash.
            AppLogger.LogWarning(
                "WebView.Nav",
                $"Could not open '{parsed.Host}' in the default browser: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
