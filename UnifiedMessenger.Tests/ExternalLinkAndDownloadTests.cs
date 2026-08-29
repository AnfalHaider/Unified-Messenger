using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Two owner-reported bugs with one cause, plus the security boundary the fix has to respect.
///
/// <para>
/// <b>What was broken.</b> <c>HandleNewWindowRequested</c> set <c>Handled = true</c> and then discarded
/// anything that was not an allow-listed http(s) host. WhatsApp decrypts a received file to a blob and
/// opens it, which arrives as a new-window request for a <c>blob:</c> URI — so <i>every download silently
/// did nothing</i>. And a customer's link, being on some other host, was discarded the same way, so
/// clicking it did nothing either. <c>HandleNavigationStarting</c> already exempted <c>blob:</c>; this path
/// never got the same treatment.
/// </para>
/// <para>
/// <b>Why the scheme list is short.</b> Handing an arbitrary scheme to the shell is how an untrusted page
/// gets local code run. These tests are the guard on that.
/// </para>
/// </summary>
public class ExternalLinkAndDownloadTests
{
    // ---- Downloads must reach WebView2 ----------------------------------------------------------------

    [Theory]
    [InlineData("blob:https://web.whatsapp.com/6f2b1c44-1f0e-4a2e-9a1b-2b5d8f0c9e77")]
    [InlineData("blob:https://mail.google.com/abcd")]
    [InlineData("data:application/pdf;base64,JVBERi0xLjQK")]
    [InlineData("data:image/jpeg;base64,/9j/4AAQSkZJRg")]
    [InlineData("BLOB:https://web.whatsapp.com/upper-case-scheme")]
    public void ADownloadIsRecognisedAndHandedBackToTheBrowser(string uri)
    {
        // The handler leaves Handled = false for these, which is what runs WebView2's own download
        // pipeline and shows the save UI. Recognising the URI is the whole fix.
        Assert.True(WebViewNavigationGuard.IsDownloadLikeUri(uri), $"'{uri}' was not treated as a download.");
    }

    [Theory]
    [InlineData("https://web.whatsapp.com/")]
    [InlineData("https://example.com/report.pdf")]
    [InlineData("about:blank")]
    [InlineData("")]
    [InlineData(null)]
    public void AnOrdinaryPageIsNotMistakenForADownload(string? uri)
    {
        Assert.False(WebViewNavigationGuard.IsDownloadLikeUri(uri));
    }

    // ---- The security boundary on shelling out --------------------------------------------------------

    [Theory]
    [InlineData("file:///C:/Windows/System32")]
    [InlineData("javascript:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ms-settings:windowsupdate")]
    [InlineData("ms-msdt:/id%20PCWDiagnostic")]
    [InlineData("search-ms:query=passwords")]
    [InlineData("shell:startup")]
    [InlineData("intent://scan/#Intent;scheme=zxing;end")]
    [InlineData("ldap://attacker/")]
    [InlineData("\\\\attacker\\share")]
    public void ADangerousSchemeIsNeverHandedToTheShell(string uri)
    {
        // A page inside a WebView2 is untrusted input. file: opens Explorer at a path of the page's
        // choosing; ms-msdt: is a documented remote-code-execution vector; shell: and search-ms: invoke
        // Windows surfaces with attacker-controlled arguments. Passing "anything that is not http" to
        // UseShellExecute would hand all of these to the operating system.
        Assert.False(
            WebViewNavigationGuard.TryOpenExternally(uri, userInitiated: true),
            $"'{uri}' would have been launched.");
    }

    [Theory]
    [InlineData("https://news.example.com/article")]
    [InlineData("http://maps.example.com/?q=dha+phase+2")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+923001234567")]
    public void TheSchemesACustomerActuallySendsAreAccepted(string uri)
    {
        // These are what a salon's customers send: an article, a map pin, an address, a number. Each one
        // used to vanish on click.
        //
        // Asserted against the scheme predicate, NOT TryOpenExternally — the first version of this test
        // called the real thing and opened four browser and mail windows on the machine running it. A unit
        // test must not have side effects outside the process.
        Assert.True(
            WebViewNavigationGuard.IsExternallyOpenableUri(uri),
            $"'{uri}' would not be forwarded to the default browser.");
    }

    [Theory]
    [InlineData("https://tracking.example.com/redirect?to=evil")]
    [InlineData("https://www.instagram.com/p/abc123/")]
    public void OnlyARealClickCanOpenABrowserWindow(string uri)
    {
        // Redirects, meta-refreshes and script navigations all reach the same handlers. If those could
        // launch the shell, any page in a monitored account becomes a pop-up cannon the owner cannot
        // close — so a launch requires the user to have clicked.
        Assert.False(WebViewNavigationGuard.TryOpenExternally(uri, userInitiated: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not a uri at all")]
    [InlineData("/relative/path")]
    public void MalformedInputIsRejectedRatherThanGuessedAt(string? uri)
    {
        Assert.False(WebViewNavigationGuard.TryOpenExternally(uri, userInitiated: true));
    }

    // ---- The in-app allowlist is unchanged ------------------------------------------------------------

    [Fact]
    public void AMonitoredSitesOwnLinksStillNavigateInPlace()
    {
        // The fix must not start throwing WhatsApp's own links at the browser — that would take the owner
        // out of the app for ordinary in-page navigation.
        Assert.True(WebViewNavigationGuard.IsAllowedNavigationUri("https://web.whatsapp.com/some/path"));
        Assert.True(WebViewNavigationGuard.IsAllowedNavigationUri("https://business.google.com/reviews"));

        // instagram.com is deliberately NOT used as the counter-example: it IS allow-listed, because the
        // Instagram platform stays registered (hidden from the picker) so existing accounts keep resolving
        // and the guard keeps their hosts. Asserting it was blocked was wrong about the app, not the fix.
        Assert.False(WebViewNavigationGuard.IsAllowedNavigationUri("https://news.example.com/article"));
    }

    // ---- A new-window request must not strand the account ----------------------------------------------

    /// <summary>
    /// The account was left on a page it could not leave.
    /// </summary>
    /// <remarks>
    /// <c>HandleNewWindowRequested</c> hopped the current frame for any ALLOW-LISTED host, and the allowlist
    /// spans each platform's whole registrable domain plus the OAuth hosts — so from WhatsApp Web it covers
    /// all of <c>whatsapp.com</c> and <c>google.com</c>. Meanwhile <c>MainWindow</c> collapses the
    /// back/forward controls whenever <c>IsPlatformModuleEnabled</c> is true, which is true for exactly the
    /// WhatsApp family. A help link, or a <c>google.com</c> link a customer sent, therefore replaced the
    /// scraped session with no way back, and oversight for that account stopped until the owner found
    /// right-click → Refresh WebView.
    /// <para>
    /// Asserted through <c>ResolveNewWindowAction</c>, which was extracted from the event handler so the
    /// decision could be tested at all — it previously needed a live <c>CoreWebView2</c>, which is why this
    /// went uncovered.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("https://faq.whatsapp.com/general/security", "https://web.whatsapp.com/")]
    [InlineData("https://www.whatsapp.com/download", "https://web.whatsapp.com/")]
    [InlineData("https://google.com/search?q=salon", "https://web.whatsapp.com/")]
    [InlineData("https://maps.google.com/place/xyz", "https://web.whatsapp.com/")]
    [InlineData("https://news.example.com/article", "https://web.whatsapp.com/")]
    public void ALinkOffTheCurrentPageOpensExternallyInsteadOfReplacingTheSession(string target, string page)
    {
        Assert.Equal(
            WebViewNavigationGuard.NewWindowAction.OpenExternally,
            WebViewNavigationGuard.ResolveNewWindowAction(target, page, isUserInitiated: true));
    }

    [Theory]
    [InlineData("https://faq.whatsapp.com/general/security")]
    [InlineData("https://www.whatsapp.com/download")]
    [InlineData("https://google.com/search?q=salon")]
    [InlineData("https://maps.google.com/place/xyz")]
    public void TheStrandingLinksWereAllOnTheAllowlist(string target)
    {
        // The other half of the proof. The old rule was "allow-listed → hop the current frame", so this
        // assertion is what made every URI above replace the scraped session. It is kept green on purpose:
        // the allowlist is still correct for its own job (deciding what the WebView may NAVIGATE to), and
        // it was reading it as a routing instruction for new-window requests that was wrong.
        Assert.True(WebViewNavigationGuard.IsAllowedNavigationUri(target));
    }

    [Theory]
    [InlineData("https://web.whatsapp.com/send?phone=123", "https://web.whatsapp.com/")]
    [InlineData("https://business.google.com/reviews?page=2", "https://business.google.com/")]
    public void ASitesOwnPageStillReplacesTheFrame(string target, string page)
    {
        // Same host: the site opened its own page in a new tab. Keeping it in-frame preserves the
        // single-window model and, for a scraped account, the session the adapter is attached to.
        Assert.Equal(
            WebViewNavigationGuard.NewWindowAction.NavigateInFrame,
            WebViewNavigationGuard.ResolveNewWindowAction(target, page, isUserInitiated: true));
    }

    [Theory]
    [InlineData("https://accounts.google.com/o/oauth2/auth", "https://business.google.com/")]
    [InlineData("https://login.microsoftonline.com/common/oauth2", "https://web.whatsapp.com/")]
    public void ASignInPopupKeepsTheCookieJar(string target, string page)
    {
        // Handing an OAuth popup to the default browser would land the session cookie in the owner's own
        // browser rather than this WebView2 profile, so the sign-in it was serving could never complete.
        Assert.Equal(
            WebViewNavigationGuard.NewWindowAction.NavigateInFrame,
            WebViewNavigationGuard.ResolveNewWindowAction(target, page, isUserInitiated: true));
    }

    [Fact]
    public void ADownloadStillReachesWebView2FromTheNewWindowPath()
    {
        Assert.Equal(
            WebViewNavigationGuard.NewWindowAction.LetWebViewHandle,
            WebViewNavigationGuard.ResolveNewWindowAction(
                "blob:https://web.whatsapp.com/6f2b1c44", "https://web.whatsapp.com/", isUserInitiated: true));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32")]
    [InlineData("ms-settings:windowsupdate")]
    public void ADangerousSchemeIsBlockedRatherThanRoutedAnywhere(string target)
    {
        Assert.Equal(
            WebViewNavigationGuard.NewWindowAction.Block,
            WebViewNavigationGuard.ResolveNewWindowAction(target, "https://web.whatsapp.com/", isUserInitiated: true));
    }

    [Fact]
    public void APageThatOpensAWindowByItselfIsNotShelledOut()
    {
        // Not user-initiated: a page opening a window on its own must never reach the shell.
        Assert.Equal(
            WebViewNavigationGuard.NewWindowAction.Block,
            WebViewNavigationGuard.ResolveNewWindowAction(
                "https://news.example.com/popup", "https://web.whatsapp.com/", isUserInitiated: false));
    }
}
