using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Pins the parse and the escalation ladder: working → degraded → broken.
/// </summary>
/// <remarks>
/// <para>Per-account assertions use a unique instance id per test, so no test can see another's streak.</para>
/// <para><b><see cref="SelectorHealth.Describe"/> is different and needs care.</b> It aggregates the whole
/// registry to answer "what is the worst state across all accounts", so a unique id does not isolate it —
/// an entry another test left behind changes the sentence. Every test that reads it therefore calls
/// <c>Reset()</c> first. That is safe only because xUnit runs the tests of one class sequentially and no
/// other class touches this type; if a second class ever records here, both need a shared collection.</para>
/// </remarks>
public class SelectorHealthTests
{
    private static string NewId() => "sel-" + Guid.NewGuid().ToString("N");

    private static string Report(
        string picks = "\"chatRow\":{\"index\":0,\"selector\":\"a\",\"count\":9}",
        string builtinUsed = "",
        string neverMatched = "",
        bool hasManifest = true,
        string ready = "true") =>
        $$"""
        {"hasManifest":{{(hasManifest ? "true" : "false")}},"observedAgainst":"test build","ready":{{ready}},
         "picks":{ {{picks}} },"builtinUsed":[{{builtinUsed}}],"neverMatched":[{{neverMatched}}],
         "missedAtLeastOnce":[]}
        """;

    // ---- Parsing ----------------------------------------------------------------------------------

    [Fact]
    public void ParsesAHealthyReport()
    {
        var entry = SelectorHealth.ParseReport(Report(), DateTimeOffset.UtcNow);

        Assert.NotNull(entry);
        Assert.True(entry!.Value.HasManifest);
        Assert.True(entry.Value.Ready);
        Assert.Equal("test build", entry.Value.ObservedAgainst);
        Assert.Equal(1, entry.Value.AnchorsResolved);
        Assert.Empty(entry.Value.DegradedAnchors);
    }

    [Fact]
    public void AnIndexAboveZeroIsDegraded()
    {
        // The whole point of the manifest: index 0 is healthy, and a rising index is the earliest warning
        // that a redesign is coming — while everything still works.
        var entry = SelectorHealth.ParseReport(
            Report(picks: "\"chatRow\":{\"index\":2,\"selector\":\"c\",\"count\":9}"),
            DateTimeOffset.UtcNow);

        Assert.Equal(["chatRow"], entry!.Value.DegradedAnchors);
    }

    [Fact]
    public void NonNumericIndicesAreNotDegraded()
    {
        // "union" is how the unread-badge anchor always reports, and "builtin" is tracked separately.
        // Treating either as a rising candidate index would make the health line permanently wrong.
        var entry = SelectorHealth.ParseReport(
            Report(picks: "\"unreadBadges\":{\"index\":\"union\",\"selector\":\"a|b\",\"count\":3}"),
            DateTimeOffset.UtcNow);

        Assert.Empty(entry!.Value.DegradedAnchors);
        Assert.Equal(1, entry.Value.AnchorsResolved);
    }

    [Fact]
    public void UnwrapsAJsonQuotedScriptResult()
    {
        // ExecuteScriptAsync returns the JSON representation of the value, so a JS string arrives quoted
        // and escaped. Parsing that without unwrapping yields nothing at all.
        var inner = Report().Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");
        var entry = SelectorHealth.ParseReport("\"" + inner + "\"", DateTimeOffset.UtcNow);

        Assert.NotNull(entry);
        Assert.True(entry!.Value.HasManifest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]      // the guarded script's answer on a page with no adapter loaded
    [InlineData("null")]
    [InlineData("{ truncated")]
    [InlineData("[1,2,3]")]
    public void AnUnusableReportYieldsNullRatherThanThrowing(string? raw)
    {
        // This runs inside a scan. Diagnostics must never be able to break the thing they diagnose.
        Assert.Null(SelectorHealth.ParseReport(raw, DateTimeOffset.UtcNow));
    }

    // ---- The escalation ladder --------------------------------------------------------------------

    [Fact]
    public void OneMissedScanIsNotBreakage()
    {
        // WhatsApp Web serves a fully loaded document with an empty chat list during a cold sync, so a
        // single all-miss report is the expected shape of a healthy account that is simply not ready.
        var id = NewId();
        SelectorHealth.Record(id, SelectorHealth.ParseReport(Report(neverMatched: "\"chatRow\""), DateTimeOffset.UtcNow)!.Value);

        Assert.Empty(SelectorHealth.BrokenAnchors(id));
        Assert.Equal(1, SelectorHealth.ConsecutiveMisses(id, "chatRow"));
    }

    [Fact]
    public void RepeatedMissesEscalateToBroken()
    {
        var id = NewId();
        for (var i = 0; i < SelectorHealth.BrokenAfterConsecutiveMisses; i++)
        {
            SelectorHealth.Record(id, SelectorHealth.ParseReport(Report(neverMatched: "\"chatRow\""), DateTimeOffset.UtcNow)!.Value);
        }

        Assert.Equal(["chatRow"], SelectorHealth.BrokenAnchors(id));
    }

    [Fact]
    public void AMatchResetsTheStreak()
    {
        // Without this a single bad scan during a cold sync would be remembered as breakage forever, and
        // the owner would be told the scraper is broken by an account that has been fine for hours.
        var id = NewId();
        for (var i = 0; i < SelectorHealth.BrokenAfterConsecutiveMisses; i++)
        {
            SelectorHealth.Record(id, SelectorHealth.ParseReport(Report(neverMatched: "\"chatRow\""), DateTimeOffset.UtcNow)!.Value);
        }

        Assert.NotEmpty(SelectorHealth.BrokenAnchors(id));

        SelectorHealth.Record(id, SelectorHealth.ParseReport(Report(), DateTimeOffset.UtcNow)!.Value);

        Assert.Empty(SelectorHealth.BrokenAnchors(id));
        Assert.Equal(0, SelectorHealth.ConsecutiveMisses(id, "chatRow"));
    }

    [Fact]
    public void BuiltinFallbackIsDegradedNotBroken()
    {
        // Falling back to the compiled-in selector means the manifest is behind the client — the numbers
        // are still right. Reporting that as breakage would be a false alarm, and a false alarm here
        // trains the owner to ignore the one warning that matters.
        var entry = SelectorHealth.ParseReport(Report(builtinUsed: "\"chatRow\""), DateTimeOffset.UtcNow);

        Assert.Equal(["chatRow"], entry!.Value.BuiltinUsed);
        Assert.Empty(entry.Value.NeverMatched);
    }

    // ---- The sentence the owner actually reads -----------------------------------------------------

    [Fact]
    public void ANotReadyPageIsReportedAsLoadingNotAsBrokenOrHealthy()
    {
        SelectorHealth.Reset();
        // The cold-sync window: readyState is "complete", the chat list is empty, and a healthy account is
        // indistinguishable from a broken selector. "Healthy" would be a claim we cannot support and
        // "broken" would be a false alarm on every launch, so the line says what is actually true.
        var id = NewId();
        SelectorHealth.Record(
            id,
            SelectorHealth.ParseReport(Report(picks: "", neverMatched: "\"chatRow\"", ready: "false"), DateTimeOffset.UtcNow)!.Value);

        var text = SelectorHealth.Describe();
        Assert.Contains("Waiting for WhatsApp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Broken", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Healthy", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OneLoadingAccountDoesNotHideThatTheOthersAreFine()
    {
        // Observed live: three accounts, one reloading, and the line said only "Waiting for WhatsApp to
        // finish loading its chat list" — hiding that the other two were healthy. On a machine with
        // several accounts there is nearly always one mid-load, so the blanket was close to permanent.
        SelectorHealth.Reset();
        SelectorHealth.Record("a", SelectorHealth.ParseReport(Report(), DateTimeOffset.UtcNow)!.Value);
        SelectorHealth.Record("b", SelectorHealth.ParseReport(Report(), DateTimeOffset.UtcNow)!.Value);
        SelectorHealth.Record("c", SelectorHealth.ParseReport(Report(picks: "", ready: "false"), DateTimeOffset.UtcNow)!.Value);

        var text = SelectorHealth.Describe();

        Assert.Contains("2 of 3", text, StringComparison.Ordinal);
        Assert.Contains("1 still loading", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADegradedAccountIsNotHiddenByALoadingOne()
    {
        // The reason degradation is checked BEFORE loading state. A real warning about account A must not
        // be suppressed indefinitely because account B happens to be waking up.
        SelectorHealth.Reset();
        SelectorHealth.Record("a", SelectorHealth.ParseReport(Report(builtinUsed: "\"chatRow\""), DateTimeOffset.UtcNow)!.Value);
        SelectorHealth.Record("b", SelectorHealth.ParseReport(Report(picks: "", ready: "false"), DateTimeOffset.UtcNow)!.Value);

        Assert.Contains("Degraded", SelectorHealth.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void APersistentlyMissingAnchorStillEscalatesEvenWhileNotReady()
    {
        // The trap in the line above: the readiness anchors ARE chatList and rowCell, so a break in those
        // makes the page report "not ready" too. If not-ready suppressed escalation, the single failure
        // that matters most could never be reported at all.
        var id = NewId();
        for (var i = 0; i < SelectorHealth.BrokenAfterConsecutiveMisses; i++)
        {
            SelectorHealth.Record(
                id,
                SelectorHealth.ParseReport(Report(picks: "", neverMatched: "\"chatRow\"", ready: "false"), DateTimeOffset.UtcNow)!.Value);
        }

        Assert.Equal(["chatRow"], SelectorHealth.BrokenAnchors(id));
    }

    [Fact]
    public void TheBreakageThresholdOutlastsAColdSync()
    {
        // Measured 2026-09-02: the chat list flapped 64 -> 0 -> 66 over ~3 minutes on a healthy account.
        // At the adaptive 25s/90s scan cadence, anything below ~8 scans can land entirely inside that
        // window and would accuse a healthy account of being broken on a slow morning.
        Assert.True(
            SelectorHealth.BrokenAfterConsecutiveMisses >= 8,
            "The threshold is short enough to fire during a normal cold sync.");
    }

    [Fact]
    public void DescribeNeverSaysBrokenForAHealthyRead()
    {
        SelectorHealth.Reset();
        var id = NewId();
        SelectorHealth.Record(id, SelectorHealth.ParseReport(Report(), DateTimeOffset.UtcNow)!.Value);

        Assert.DoesNotContain("Broken", SelectorHealth.Describe(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeSaysReadingIsStillCorrectWhenMerelyDegraded()
    {
        SelectorHealth.Reset();
        // The distinction the whole class exists for: degraded is a warning about the FUTURE, not a claim
        // that today's numbers are wrong. If this sentence ever stops saying so, the owner will read a
        // fallback selector as lost data.
        var id = NewId();
        SelectorHealth.Record(id, SelectorHealth.ParseReport(Report(builtinUsed: "\"chatRow\""), DateTimeOffset.UtcNow)!.Value);

        var text = SelectorHealth.Describe();
        Assert.Contains("Still reading correctly", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeUsesPlainLanguageNotSelectorJargon()
    {
        SelectorHealth.Reset();
        // Shown to a paying customer. "querySelector", "anchor", "DOM" and "index" are our words.
        var id = NewId();
        SelectorHealth.Record(id, SelectorHealth.ParseReport(Report(builtinUsed: "\"chatRow\""), DateTimeOffset.UtcNow)!.Value);

        var text = SelectorHealth.Describe();
        foreach (var jargon in new[] { "querySelector", "DOM", "anchor", "candidate index", "manifest" })
        {
            Assert.DoesNotContain(jargon, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheReportScriptIsSafeOnAPageWithNoAdapter()
    {
        // It runs on every scan, including on a page mid-boot. An unguarded call would throw a
        // ReferenceError into the scan path on every launch.
        Assert.Contains("window.__umSelectorReport ?", SelectorHealth.ReportScript);
    }
}
