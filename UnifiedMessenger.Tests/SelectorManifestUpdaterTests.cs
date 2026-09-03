using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Guards the trust boundary. Everything here is a rule about a file that arrives from outside the build.
/// </summary>
public class SelectorManifestUpdaterTests
{
    private const string Good = """
        { "schemaVersion": 1, "platform": "whatsapp", "observedAgainst": "shipped fix",
          "anchors": { "chatRow": { "candidates": ["#pane-side [role=\"row\"]"] } } }
        """;

    // ---- Which assets are even considered ----------------------------------------------------------

    [Theory]
    [InlineData("selectors-whatsapp.json", "whatsapp")]
    [InlineData("selectors-googlebusiness.json", "googlebusiness")]
    [InlineData("SELECTORS-WHATSAPP.JSON", "whatsapp")]
    public void RecognisesAManifestAssetForAKnownPlatform(string asset, string expected) =>
        Assert.Equal(expected, SelectorManifestUpdater.PlatformFromAssetName(asset));

    [Theory]
    [InlineData("UnifiedMessengerSetup.exe")]
    [InlineData("selectors-.json")]
    [InlineData("selectors-notaplatform.json")]   // a platform this build cannot validate is one it must not write
    [InlineData("selectors-whatsapp.exe")]
    [InlineData("selectors-whatsapp.json.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void IgnoresEverythingElse(string? asset) =>
        Assert.Null(SelectorManifestUpdater.PlatformFromAssetName(asset));

    // ---- Where it may be fetched from --------------------------------------------------------------

    [Theory]
    [InlineData("https://github.com/o/r/releases/download/v1/selectors-whatsapp.json")]
    [InlineData("https://objects.githubusercontent.com/x")]
    [InlineData("https://release-assets.githubusercontent.com/x")]
    public void AcceptsGitHubOverHttps(string url) =>
        Assert.True(SelectorManifestUpdater.IsTrustedAssetUrl(url));

    [Theory]
    [InlineData("http://github.com/x")]                  // plaintext
    [InlineData("https://evil.example/x")]               // another host entirely
    [InlineData("https://github.com.evil.example/x")]    // suffix trick
    [InlineData("https://notgithub.com/x")]
    [InlineData("file:///C:/x.json")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesAnythingElse(string? url) =>
        Assert.False(SelectorManifestUpdater.IsTrustedAssetUrl(url));

    [Fact]
    public void TheHostCheckIsStricterThanTheInstallerPath()
    {
        // The installer download only checks that a URL is HTTPS. That is looser than it should be, and a
        // NEW outbound path must not inherit the gap by copying it. If someone ever "simplifies" this to
        // match, the difference disappears silently — so it is asserted.
        const string httpsButNotGitHub = "https://example.com/selectors-whatsapp.json";

        Assert.True(GitHubUpdateService.IsValidDownloadUrl(httpsButNotGitHub));
        Assert.False(SelectorManifestUpdater.IsTrustedAssetUrl(httpsButNotGitHub));
    }

    // ---- What it will accept as a manifest ---------------------------------------------------------

    [Fact]
    public void AcceptsAWellFormedManifest()
    {
        Assert.True(SelectorManifestUpdater.Validate(Good, "whatsapp", out var manifest, out var reason), reason);
        Assert.Equal("shipped fix", manifest!.ObservedAgainst);
    }

    [Theory]
    // Truncated download, or a half-written file.
    [InlineData("{ not json")]
    // Aimed at another platform: a mis-delivered asset must not be applied to the wrong client.
    [InlineData("""{ "schemaVersion": 1, "platform": "telegram", "anchors": {} }""")]
    // A schema this build does not understand.
    [InlineData("""{ "schemaVersion": 99, "platform": "whatsapp", "anchors": {} }""")]
    // Configured-looking but blind: an anchor answering with nothing suppresses the JS fallback.
    [InlineData("""{ "schemaVersion": 1, "platform": "whatsapp", "anchors": { "chatRow": { "candidates": [] } } }""")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesAManifestItCannotVouchFor(string? json)
    {
        Assert.False(SelectorManifestUpdater.Validate(json, "whatsapp", out var manifest, out _));
        Assert.Null(manifest);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JAVASCRIPT:alert(1)")]
    [InlineData("width:expression(alert(1))")]
    public void RefusesASelectorThatIsNotASelector(string candidate)
    {
        // A CSS selector contains none of these. Their presence means the file is not what it claims to
        // be, and the right response is to refuse it rather than reason about whether this particular
        // one could do harm.
        var json = $$"""
            { "schemaVersion": 1, "platform": "whatsapp", "anchors":
              { "chatRow": { "candidates": ["{{candidate.Replace("\"", "\\\"")}}"] } } }
            """;

        Assert.False(SelectorManifestUpdater.Validate(json, "whatsapp", out _, out var reason));
        Assert.NotEqual(string.Empty, reason);
    }

    [Fact]
    public void RefusesAnAbsurdlyLargeManifest()
    {
        var padding = new string('x', SelectorManifestUpdater.MaxManifestBytes);
        var json = $$"""
            { "schemaVersion": 1, "platform": "whatsapp", "notes": "{{padding}}",
              "anchors": { "chatRow": { "candidates": ["#pane-side"] } } }
            """;

        Assert.False(SelectorManifestUpdater.Validate(json, "whatsapp", out _, out _));
    }

    [Fact]
    public void RefusesAnOverLengthSelector()
    {
        var json = $$"""
            { "schemaVersion": 1, "platform": "whatsapp",
              "anchors": { "chatRow": { "candidates": ["{{new string('a', 600)}}"] } } }
            """;

        Assert.False(SelectorManifestUpdater.Validate(json, "whatsapp", out _, out var reason));
        Assert.Contains("over-length", reason);
    }

    // ---- What it does with them --------------------------------------------------------------------

    [Fact]
    public async Task InstallsNothingWhenThereAreNoManifestAssets()
    {
        var installed = await SelectorManifestUpdater.TryApplyAsync(
            new Dictionary<string, string>(),
            (_, _) => throw new InvalidOperationException("must not download"),
            CancellationToken.None);

        Assert.Equal(0, installed);
    }

    [Fact]
    public async Task DoesNotEvenDownloadFromAnUntrustedHost()
    {
        // The host check has to happen before the request, not after: a fetch from an attacker-chosen
        // host is itself the problem, whatever the response body turns out to be.
        var installed = await SelectorManifestUpdater.TryApplyAsync(
            new Dictionary<string, string> { ["selectors-whatsapp.json"] = "https://evil.example/x.json" },
            (_, _) => throw new InvalidOperationException("must not download"),
            CancellationToken.None);

        Assert.Equal(0, installed);
    }

    [Fact]
    public async Task ADownloadFailureIsSwallowedRatherThanBreakingTheUpdateCheck()
    {
        var installed = await SelectorManifestUpdater.TryApplyAsync(
            new Dictionary<string, string> { ["selectors-whatsapp.json"] = "https://github.com/o/r/x.json" },
            (_, _) => throw new HttpRequestException("network down"),
            CancellationToken.None);

        Assert.Equal(0, installed);
    }

    [Fact]
    public async Task AnInvalidManifestIsNeverWritten()
    {
        // The property that matters most: validation happens BEFORE the file reaches disk, so a bad
        // download cannot leave the next launch reading a broken manifest.
        var path = UnifiedMessenger.Services.Adapters.SelectorManifestLoader.OverridePath("whatsapp");
        var existedBefore = File.Exists(path);

        var installed = await SelectorManifestUpdater.TryApplyAsync(
            new Dictionary<string, string> { ["selectors-whatsapp.json"] = "https://github.com/o/r/x.json" },
            (_, _) => Task.FromResult<string?>("{ truncated"),
            CancellationToken.None);

        Assert.Equal(0, installed);
        Assert.Equal(existedBefore, File.Exists(path));
    }
}
