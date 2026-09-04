namespace UnifiedMessenger.Tests;

/// <summary>
/// Guards the sign-in handshake (A12).
///
/// <para>
/// Two assertions in this class previously pinned the defect rather than the behaviour: one required
/// <c>web.whatsapp.com</c> to appear in the script — the URL shortcut that made an account parked on the
/// QR screen report "Connected" — and another asserted <c>googlebusiness</c> was <i>absent</i>, which
/// locked in the missing profile. Both are inverted below, with the reasoning kept so neither is
/// restored by someone reading the old test as intent.
/// </para>
/// </summary>
public class ConnectionHandshakeScriptTests
{
    [Fact]
    public void ConnectionHandshakeScript_PostsConnectionStatusMessage()
    {
        var script = ReadScript("connection-handshake.js");

        Assert.Contains("type: 'connection-status'", script, StringComparison.Ordinal);
        Assert.Contains("__umStartConnectionHandshake", script, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("'Connected'", script, StringComparison.Ordinal);
        Assert.Contains("__umConnectionPollTimer", script, StringComparison.Ordinal);
        Assert.Contains("whatsapp", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoggedOutIsEvaluatedBeforeLoggedIn()
    {
        var script = ReadScript("connection-handshake.js");
        var body = ExtractEvaluateConnection(script);

        var loggedOutAt = body.IndexOf("profile.loggedOut", StringComparison.Ordinal);
        var loggedInAt = body.IndexOf("profile.loggedIn", StringComparison.Ordinal);

        Assert.True(loggedOutAt >= 0, "evaluateConnection must test the logged-out anchors.");
        Assert.True(loggedInAt >= 0, "evaluateConnection must test the logged-in anchors.");

        // The whole defect in one assertion. Sign-in markup is specific (a QR canvas, a password field);
        // signed-in markup is generic, and a login page carries plenty of it — the generic profile's own
        // logged-in test is 'main, nav, header'. Asking "signed in?" first therefore answers yes on a
        // sign-in screen and never reaches the question that would have said no.
        Assert.True(
            loggedOutAt < loggedInAt,
            "evaluateConnection must test logged-out BEFORE logged-in, or a sign-in screen reports Connected.");
    }

    [Fact]
    public void NoPlatformTreatsItsOwnHostAsProofOfASession()
    {
        var script = ReadScript("connection-handshake.js");

        // web.whatsapp.com was in urlLoggedIn, so merely being on the host counted as being signed in and
        // the QR check below it never ran. A URL says which client is loaded, never whether anyone is
        // signed into it — so no platform host may appear as a logged-IN hint.
        Assert.DoesNotContain("urlLoggedIn: ['web.whatsapp.com']", script, StringComparison.Ordinal);
        Assert.DoesNotContain("urlLoggedIn: ['instagram.com']", script, StringComparison.Ordinal);
        Assert.DoesNotContain("urlLoggedIn: ['messenger.com']", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleMaySpecifyALoggedOutUrlBecauseItRedirectsToItsOwnSignInHost()
    {
        var script = ReadScript("connection-handshake.js");

        // The one place a URL IS evidence, and it points the safe way: Google sends an unauthenticated
        // session to accounts.google.com, so landing there is a positive logged-OUT signal rather than an
        // inference about a session that may not exist.
        Assert.Contains("urlLoggedOut", script, StringComparison.Ordinal);
        Assert.Contains("accounts.google.com/signin", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("instagram")]
    [InlineData("messenger")]
    [InlineData("googlebusiness")]
    public void EveryConnectedChannelHasItsOwnProfile(string platformId)
    {
        var script = ReadScript("connection-handshake.js");

        // Without a profile these fell through to `generic`, whose logged-in test is 'main, nav, header' —
        // markup most login pages also carry. This test previously asserted googlebusiness was ABSENT.
        Assert.Contains($"{platformId}: {{", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppBusinessSharesWhatsAppAnchorsRatherThanFallingThroughToGeneric()
    {
        var script = ReadScript("connection-handshake.js");

        // It runs the identical web client. Falling through to generic would match the QR screen on
        // 'nav, header' and report a signed-out Business account as connected.
        Assert.Contains("profiles.whatsappbusiness = profiles.whatsapp", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstagramDoesNotTreatTheFeedItselfAsASignInSignal()
    {
        var script = ReadScript("connection-handshake.js");
        var profile = ExtractProfile(script, "instagram");

        // instagram.com serves a logged-out feed of public content, so 'main' or an article would match on
        // a page nobody is signed into. The anchors must be session-bearing chrome.
        Assert.Contains("a[href=\"/direct/inbox/\"]", profile, StringComparison.Ordinal);
        Assert.Contains("input[name=\"username\"]", profile, StringComparison.Ordinal);
    }

    private static string ExtractEvaluateConnection(string script)
    {
        var start = script.IndexOf("function evaluateConnection", StringComparison.Ordinal);
        Assert.True(start >= 0, "evaluateConnection must exist.");
        var end = script.IndexOf("window.__umStartConnectionHandshake", start, StringComparison.Ordinal);
        return end > start ? script[start..end] : script[start..];
    }

    private static string ExtractProfile(string script, string platformId)
    {
        var start = script.IndexOf($"{platformId}: {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The {platformId} profile must exist.");
        var end = script.IndexOf("urlLoggedIn", start, StringComparison.Ordinal);
        return end > start ? script[start..end] : script[start..];
    }

    private static string ReadScript(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", fileName);
        return File.ReadAllText(path);
    }
}
