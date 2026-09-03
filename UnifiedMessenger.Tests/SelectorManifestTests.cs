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
        // WhatsApp Web's row classes are hashed build artifacts and anchoring on one looks fine the day it
        // is written, then breaks silently at the next deploy. Its *semantic* classes are plain words
        // (`message-out`, `selectable-text`, `title`) and are legitimate anchors the shipped scraper has
        // read for the life of the feature — so "has no hyphen" is the wrong rule; it fails `.title`.
        //
        // What actually separates the two is shape: every hash observed here either starts with an
        // underscore (`_ak8k`) or carries digits among the letters (`x1n2onr6`). Words do neither.
        var hashedClass = new System.Text.RegularExpressions.Regex(@"\.(_[A-Za-z0-9_]+|[A-Za-z]*[0-9][A-Za-z0-9]*)\b");

        foreach (var (name, anchor) in WhatsApp().Anchors)
        {
            foreach (var candidate in anchor.Candidates)
            {
                var match = hashedClass.Match(candidate);
                Assert.False(
                    match.Success,
                    $"Anchor '{name}' depends on '{match.Value}', which has the shape of a hashed build artifact: {candidate}");
            }
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

    // ---- A3 migration guards ----------------------------------------------------------------------

    private static string ScriptText(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", file));

    [Fact]
    public void EveryAnchorNameUsedByTheScriptsExistsInTheManifest()
    {
        // The failure this migration could introduce and nothing else would catch: a typo'd anchor name.
        // __umPick falls back to the built-in selector, so a misspelling still WORKS — it just silently
        // stops being manifest-driven, and the whole point of the increment quietly evaporates.
        var source = ScriptText("whatsapp-adapter.js") + "\n" + ScriptText("adapter-core.js");
        var referenced = System.Text.RegularExpressions.Regex
            .Matches(source, @"(?:__umPick1?|__umPickIn1?|umCandidates)\(\s*(?:[A-Za-z0-9_\[\]().]+,\s*)?'([a-zA-Z][A-Za-z0-9]*)'")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(referenced);

        var anchors = WhatsApp().Anchors;
        var missing = referenced.Where(r => !anchors.ContainsKey(r)).ToList();
        Assert.True(missing.Count == 0, "Scripts reference anchors absent from the manifest: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryManifestAnchorIsEitherUsedOrDeclaredReady()
    {
        // Dead config is worse than no config: it reads as coverage the scraper does not actually have.
        // An anchor earns its place by being called, or by gating readiness.
        // The readback script is a private const in the app assembly. Read it by reflection rather than
        // from a source path: the path arithmetic from the test output directory is fragile, and it broke
        // the first time this test ran.
        var readbackScript = (string?)typeof(ConversationFocusHelper)
            .GetField("OpenChatHeaderScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.GetRawConstantValue() ?? string.Empty;
        Assert.NotEqual(string.Empty, readbackScript);

        // The C# navigators are consumers too, and their scripts are private consts — read them the same
        // way. Anchors named by a NavigationOperation's readback count as used as well: that is a real
        // consumer even though the anchor name never appears in any script text.
        var navigatorScripts = new[] { "OpenScript", "CloseScript" }
            .Select(f => (string?)typeof(ArchivedPanelNavigator)
                .GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.GetRawConstantValue() ?? string.Empty);

        var readbackAnchors = NavigationOperations.All.SelectMany(o => o.ReadbackAnchors);

        var source = ScriptText("whatsapp-adapter.js") + "\n" + ScriptText("adapter-core.js") + "\n"
                     + readbackScript + "\n" + string.Join("\n", navigatorScripts) + "\n"
                     + string.Join("\n", readbackAnchors.Select(a => $"'{a}'"));

        var manifest = WhatsApp();
        var ready = manifest.ReadyWhen?.All ?? [];

        // Anchors measured and recorded for a reader that does not exist yet. Each must name its increment,
        // so this list can only shrink deliberately.
        string[] declaredForLater =
        [
            "ackGlyph",            // A9-era: CanReadAck is still false, no reader may exist before it flips
            "rowPreview",          // A7 navigation readback
            "rowMeta",             // A7 navigation readback
            "archivedButton",      // A7: "show archived" is a named operation, not yet built
            "searchContainer"      // A7: search-driven navigation
        ];

        var unused = manifest.Anchors.Keys
            .Where(k => !ready.Contains(k) && !declaredForLater.Contains(k) && !source.Contains($"'{k}'"))
            .ToList();

        Assert.True(unused.Count == 0, "Manifest anchors with no consumer: " + string.Join(", ", unused));
    }

    [Fact]
    public void MigratedCallSitesStillPassTheirBuiltInSelector()
    {
        // __umPick(name) with no second argument cannot fall back, so a manifest that fails to load would
        // leave that call site blind. Every migrated site must keep the selector it used to hardcode.
        var source = ScriptText("whatsapp-adapter.js") + "\n" + ScriptText("adapter-core.js");
        var oneArg = System.Text.RegularExpressions.Regex
            .Matches(source, @"window\.__umPick1?\(\s*'([a-zA-Z][A-Za-z0-9]*)'\s*\)")
            .Select(m => m.Groups[1].Value)
            .Where(n => n != "required")   // __umSelectorsReady probes readiness anchors by name, no DOM fallback wanted
            .ToList();

        Assert.True(oneArg.Count == 0, "These call sites have no built-in fallback: " + string.Join(", ", oneArg));
    }

    [Fact]
    public void TheUnreadBadgeAnchorKeepsUnionSemantics()
    {
        // countFromDomBadges SUMS across three badge markups. First-match semantics here would not fail
        // visibly — it would silently undercount unread chats, which is the metric the product exists for.
        var badges = WhatsApp().Anchors["unreadBadges"];

        Assert.Equal("union", badges.Match);
        Assert.Equal(3, badges.Candidates.Count);
    }

    [Fact]
    public void AnchorsThatCannotMatchOnAHealthyAccountAreMarkedOptional()
    {
        // Every one of these was observed in `neverMatched` on a live, perfectly healthy account: the
        // conversation-scoped ones because no chat was open (and the read-only rule forbids opening one to
        // check), and `rowConversationId` because `[data-id]` is absent from the entire current document.
        // Unmarked, each would escalate to "broken" on every install after three scans.
        string[] mustBeOptional =
        [
            "conversationRoot", "conversationTitle", "conversationSubtitle", "conversationProfilePhone",
            "messageContainer", "rowConversationId", "selectedChatRow", "openChatPane", "composer"
        ];

        var anchors = WhatsApp().Anchors;
        foreach (var name in mustBeOptional)
        {
            Assert.True(anchors.ContainsKey(name), $"'{name}' is missing from the manifest.");
            Assert.True(anchors[name].Optional, $"'{name}' would raise a false 'broken' alarm on a healthy account.");
        }
    }

    [Fact]
    public void TheAnchorsThatDoEscalateAreTheOnesThatMeanTheChatListIsUnreadable()
    {
        // The complement of the rule above, asserted directly: if this set ever empties, the health
        // surface can no longer report breakage at all and would sit permanently green.
        var escalating = WhatsApp().Anchors.Where(a => !a.Value.Optional).Select(a => a.Key).ToList();

        Assert.Contains("chatRow", escalating);
        Assert.Contains("chatList", escalating);
        Assert.Contains("rowTitle", escalating);
        Assert.True(escalating.Count >= 10, $"Only {escalating.Count} anchors can report breakage.");
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
        // Not migrated, and not going to be: Discord is an embed-only tab with no scraper to configure.
        // (googlebusiness was on this list until A6 gave it a manifest — see GoogleSelectorManifestTests.)
        Assert.Null(SelectorManifestLoader.ForPlatform("discord"));
        Assert.Null(SelectorManifestLoader.ForPlatform("telegram"));
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
