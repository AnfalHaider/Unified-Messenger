using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Pins the selector manifest schema, the built-in default, and the degrade-never-die load order.
/// </summary>
/// <remarks>
/// The load-order tests drive <see cref="SelectorManifestLoader.Resolve"/> directly with strings rather
/// than writing override files. That is deliberate: xUnit runs test classes in parallel, and a test that
/// wrote into <c>ApplicationPaths.UserDataRoot</c> and cleared the loader cache would hand a half-built
/// state to anything else resolving a manifest in the same run — the same trap AGENTS.md records for
/// <c>UserDataRootOverrideForTests</c>.
/// </remarks>
public class SelectorManifestTests
{
    private static SelectorManifest WhatsApp() =>
        SelectorManifestLoader.ForPlatform("whatsapp")
        ?? throw new InvalidOperationException("The built-in WhatsApp selector manifest did not load.");

    [Fact]
    public void TheBuiltInWhatsAppManifestLoads()
    {
        var manifest = WhatsApp();

        Assert.Equal(SelectorManifestLoader.SupportedSchemaVersion, manifest.SchemaVersion);
        Assert.Equal("whatsapp", manifest.Platform);
        Assert.NotEmpty(manifest.Anchors);
    }

    [Fact]
    public void EveryAnchorHasAtLeastOneCandidate()
    {
        // An anchor with no candidates resolves to nothing while LOOKING configured, and the JS fallback
        // only engages for an anchor the manifest does not answer at all. Silent blindness.
        foreach (var (name, anchor) in WhatsApp().Anchors)
        {
            Assert.True(anchor.Candidates.Count > 0, $"Anchor '{name}' has no candidates.");
            Assert.All(anchor.Candidates, c => Assert.False(string.IsNullOrWhiteSpace(c)));
        }
    }

    [Fact]
    public void NoCandidateDependsOnAHashedClassName()
    {
        // WhatsApp Web's row classes are hashed build artifacts. A manifest that anchors on one looks
        // fine on the day it is written and breaks silently at the next deploy.
        foreach (var (name, anchor) in WhatsApp().Anchors)
        {
            Assert.All(
                anchor.Candidates,
                c => Assert.False(c.Contains('.') && !c.Contains('['), $"Anchor '{name}' looks class-anchored: {c}"));
        }
    }

    [Fact]
    public void ManifestRecordsWhatItWasObservedAgainst()
    {
        // An inventory with no version is undatable and therefore untrustworthy within six months; the
        // same is true of the manifest derived from it.
        Assert.False(string.IsNullOrWhiteSpace(WhatsApp().ObservedAgainst));
    }

    [Fact]
    public void ReadinessAnchorsExistInTheManifest()
    {
        var manifest = WhatsApp();

        Assert.NotNull(manifest.ReadyWhen);
        Assert.NotEmpty(manifest.ReadyWhen!.All);
        foreach (var required in manifest.ReadyWhen.All)
        {
            Assert.True(manifest.Anchors.ContainsKey(required), $"readyWhen names '{required}', which is not an anchor.");
        }
    }

    [Fact]
    public void TheAckAnchorReadsColourNotTheIconName()
    {
        // Measured live 2026-09-02: WhatsApp's delivered and read ticks are BOTH titled "wds-ic-read";
        // only the computed fill separates them. This is the same defect class that labelled five
        // unanswered one-star Google reviews "Positive" for the life of that feature. If someone
        // "simplifies" this anchor to read the name, this test is what stops it.
        var ack = WhatsApp().Anchors["ackGlyph"];

        Assert.Equal("fill", ack.Read);
        Assert.Equal("wds-ic-read", ack.RequireTitle);
        Assert.NotNull(ack.States);
        Assert.Contains("rgb(0, 123, 252)", ack.States!["read"]);
        Assert.Contains("rgba(0, 0, 0, 0.6)", ack.States["delivered"]);
    }

    [Fact]
    public void WhatsAppBusinessSharesTheWhatsAppManifest()
    {
        Assert.Equal("whatsapp", SelectorManifestLoader.ManifestIdFor("whatsappbusiness"));
        Assert.NotNull(SelectorManifestLoader.ForPlatform("whatsappbusiness"));
    }

    [Fact]
    public void PlatformsWithoutAManifestReturnNullRatherThanThrowing()
    {
        // Not yet migrated. The injection then emits nothing and every JS call site uses its built-in.
        Assert.Null(SelectorManifestLoader.ForPlatform("discord"));
        Assert.Null(SelectorManifestLoader.ForPlatform("googlebusiness"));
    }

    [Fact]
    public void TheInjectionScriptIsAssignableJavaScript()
    {
        var script = SelectorManifestLoader.BuildInjectionScript("whatsapp");

        Assert.StartsWith("window.__umSelectors = {", script);
        Assert.EndsWith("};", script);

        // The payload must be parseable JSON, or the page throws on document-created and the whole
        // adapter chain after it never runs.
        var json = script["window.__umSelectors = ".Length..].TrimEnd(';');
        var round = JsonSerializer.Deserialize<SelectorManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("whatsapp", round!.Platform);
    }

    [Fact]
    public void APlatformWithNoManifestInjectsNothing() =>
        Assert.Equal(string.Empty, SelectorManifestLoader.BuildInjectionScript("discord"));

    // ---- Load order: an override may only ever be ignored, never fatal -----------------------------

    private const string Embedded = """
        { "schemaVersion": 1, "platform": "whatsapp", "observedAgainst": "built-in",
          "anchors": { "chatRow": { "candidates": ["#pane-side [role=\"row\"]"] } } }
        """;

    [Fact]
    public void AValidOverrideWins()
    {
        const string over = """
            { "schemaVersion": 1, "platform": "whatsapp", "observedAgainst": "override",
              "anchors": { "chatRow": { "candidates": [".new-thing"] } } }
            """;

        Assert.Equal("override", SelectorManifestLoader.Resolve("whatsapp", over, Embedded)!.ObservedAgainst);
    }

    [Theory]
    // Malformed - a truncated download, or a half-written file.
    [InlineData("{ not json at all ")]
    // A schema this build does not understand. Shipping a v2 manifest to a v1 binary must not brick it.
    [InlineData("""{ "schemaVersion": 99, "platform": "whatsapp", "anchors": {} }""")]
    // Aimed at another platform - a mis-delivered file must not be applied to the wrong client.
    [InlineData("""{ "schemaVersion": 1, "platform": "telegram", "anchors": {} }""")]
    // Configured-looking but blind: an anchor that answers with nothing suppresses the JS fallback.
    [InlineData("""{ "schemaVersion": 1, "platform": "whatsapp", "anchors": { "chatRow": { "candidates": [] } } }""")]
    [InlineData("")]
    [InlineData(null)]
    public void ABadOverrideFallsBackToTheBuiltInRatherThanFailing(string? overrideJson)
    {
        var resolved = SelectorManifestLoader.Resolve("whatsapp", overrideJson, Embedded);

        Assert.NotNull(resolved);
        Assert.Equal("built-in", resolved!.ObservedAgainst);
    }

    [Fact]
    public void WhenNeitherSourceParsesTheResultIsNullNotAnException()
    {
        // The floor: no manifest at all. The page gets no injection and the scrapers run exactly as they
        // did before the manifest existed. Degraded, never dead.
        Assert.Null(SelectorManifestLoader.Resolve("whatsapp", "{ broken", "{ also broken"));
    }

    [Fact]
    public void TheOverridePathLivesInUserDataNotTheInstallDirectory()
    {
        // It has to be writable by the update channel and survive reinstalls; the install directory is
        // neither. It also must not be somewhere an agent shell would fork under MSIX redirection.
        var path = SelectorManifestLoader.OverridePath("whatsappbusiness");

        Assert.StartsWith(ApplicationPaths.UserDataRoot, path);
        Assert.EndsWith(Path.Combine("selectors", "whatsapp.json"), path);
    }
}
