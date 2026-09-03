using System.Text.RegularExpressions;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The second channel on the manifest, and therefore the test of whether the schema generalises.
/// </summary>
/// <remarks>
/// WhatsApp's anchors are all CSS selectors resolved by an injected runtime. Google has neither: its
/// pages get no adapter-core (googlebusiness routes to <c>NullPlatformAdapter</c>), and its most volatile
/// knowledge is not a selector at all but the set of <b>text patterns</b> that pull a lifetime review
/// total out of the page. If the schema could only describe CSS, or could only be read by
/// <c>__umPick</c>, this is where that would have shown.
/// </remarks>
public class GoogleSelectorManifestTests
{
    private static SelectorManifest Google() =>
        SelectorManifestLoader.ForPlatform("googlebusiness")
        ?? throw new InvalidOperationException("The built-in Google selector manifest did not load.");

    [Fact]
    public void TheGoogleManifestLoads()
    {
        var manifest = Google();

        Assert.Equal(SelectorManifestLoader.SupportedSchemaVersion, manifest.SchemaVersion);
        Assert.Equal("googlebusiness", manifest.Platform);
        Assert.False(string.IsNullOrWhiteSpace(manifest.ObservedAgainst));
    }

    [Fact]
    public void RegexAnchorsDeclareThemselvesAsRegex()
    {
        // The field the second channel forced into the schema. A validator or a reader that assumed CSS
        // would mangle these, and nothing about the strings themselves would give the mistake away.
        foreach (var name in new[] { "ratingAriaLabel", "reviewTotalPaired", "reviewTotalUnpaired" })
        {
            Assert.True(Google().Anchors[name].IsRegex, $"'{name}' must declare kind: regex.");
        }
    }

    [Fact]
    public void CssAnchorsDoNotClaimToBeRegex()
    {
        foreach (var name in new[] { "rowsPerPageControl", "rowsPerPageOption", "reviewCard" })
        {
            Assert.False(Google().Anchors[name].IsRegex, $"'{name}' is a CSS selector.");
        }
    }

    [Fact]
    public void EveryRegexCandidateActuallyCompiles()
    {
        // These ship as data and are compiled in the page. A pattern that cannot compile would be skipped
        // silently at runtime, quietly costing a layout nobody would notice was missing.
        foreach (var (name, anchor) in Google().Anchors.Where(a => a.Value.IsRegex))
        {
            foreach (var candidate in anchor.Candidates)
            {
                var ex = Record.Exception(() => new Regex(candidate, RegexOptions.IgnoreCase));
                Assert.True(ex is null, $"'{name}' has a pattern that will not compile: {candidate} ({ex?.Message})");
            }
        }
    }

    [Fact]
    public void BothMeasuredReviewCountLayoutsAreCarried()
    {
        // The finding that justifies putting these in a manifest at all: measured 2026-09-02, a profile
        // AGENTS.md recorded as bracketed-only was rendering the labelled layout instead. Layout varies per
        // profile AND over time, so both must always be present and neither may be described as "the"
        // layout for anything.
        var paired = Google().Anchors["reviewTotalPaired"].Candidates;

        Assert.Equal(2, paired.Count);
        Assert.Contains(paired, p => p.Contains("Google", StringComparison.Ordinal));   // labelled
        Assert.Contains(paired, p => p.Contains(@"\(", StringComparison.Ordinal));      // bracketed
    }

    [Fact]
    public void EveryPairedPatternCapturesRatingThenTotal()
    {
        // The script reads group 1 as the rating and group 2 as the total. A pattern with a different group
        // shape would not fail — it would report the wrong numbers, which is worse.
        foreach (var candidate in Google().Anchors["reviewTotalPaired"].Candidates)
        {
            var match = new Regex(candidate, RegexOptions.IgnoreCase).Match("4.6 ★ (991) · Salon 4.6 ★ 239 Google reviews");
            Assert.True(match.Success, $"Pattern matched neither layout: {candidate}");
            Assert.True(match.Groups.Count >= 3, $"Pattern must capture rating and total: {candidate}");
        }
    }

    [Fact]
    public void TheUnpairedPatternCapturesOnlyTheTotal()
    {
        var candidate = Google().Anchors["reviewTotalUnpaired"].Candidates.Single();
        var match = new Regex(candidate, RegexOptions.IgnoreCase).Match("Reviews\n239 Google reviews");

        Assert.True(match.Success);
        Assert.Equal(2, match.Groups.Count);   // whole match + the total
        Assert.Equal("239", match.Groups[1].Value);
    }

    [Fact]
    public void TheStarRatingIsNotCarriedAsColourData()
    {
        // Deliberate, and contrary to the plan this increment was written from. The shipped star reader
        // compares each glyph to the FIRST star's own colour rather than to a known gold, which is
        // self-calibrating: a Google restyle changes nothing. Moving those colours into the manifest would
        // have replaced a mechanism that cannot go stale with two hard-coded values that can — a
        // regression dressed as configuration. If someone adds them later, this says why not to.
        var anchors = Google().Anchors;

        Assert.DoesNotContain(anchors, a => a.Value.States is not null);
        Assert.DoesNotContain(anchors, a => a.Key.Contains("star", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheGoogleManifestIsInjectable()
    {
        // Google pages have no __umPick — the manifest reaches them as window.__umSelectors ahead of the
        // kickoff scripts, and __umGRSel reads it there.
        var script = SelectorManifestLoader.BuildInjectionScript("googlebusiness");

        Assert.StartsWith("window.__umSelectors = {", script);
        Assert.Contains("reviewTotalPaired", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RatingKickoff")]
    [InlineData("KickoffScript")]
    public void EveryScriptThatUsesTheManifestAlsoDefinesTheHelper(string constName)
    {
        // Found the hard way. RatingKickoff does not include PageHelpers — it is a separate, lighter script
        // on a different page — so when the manifest lookup was defined only in PageHelpers, __umGRRx was
        // undefined on the rating path. It threw inside the script's own try/catch and surfaced as
        // state:'error' with no clue why, and a probe showed it working because the reviews script had
        // already run on that tab and defined the helper. The failure depended on which script ran first.
        var script = (string?)typeof(GoogleReviewSnapshotService)
            .GetField(constName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.GetRawConstantValue();

        Assert.False(string.IsNullOrWhiteSpace(script), $"{constName} is not a string const any more.");

        if (script!.Contains("__umGRRx(", StringComparison.Ordinal) ||
            script.Contains("__umGRSel(", StringComparison.Ordinal))
        {
            Assert.True(
                script.Contains("window.__umGRRx=function", StringComparison.Ordinal),
                $"{constName} calls the manifest helper without defining it — it will throw on any page "
                + "where no other script happened to run first.");
        }
    }

    [Fact]
    public void ProseIsNotShippedIntoThePage()
    {
        // Every manifest is serialized into the page on each load. The `notes` in these files run to
        // several hundred characters and exist for whoever edits them next, so they are deliberately not
        // modelled and never reach a browser.
        var script = SelectorManifestLoader.BuildInjectionScript("googlebusiness");

        Assert.DoesNotContain("varies per profile", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"notes\"", script, StringComparison.OrdinalIgnoreCase);
    }
}
