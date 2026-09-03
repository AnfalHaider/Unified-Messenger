using System.Reflection;
using System.Text.RegularExpressions;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The lifetime review total is parsed out of the Google Search merchant view by a regex chain that ships as
/// JavaScript inside a C# const. This runs that shipped chain against the exact text Google renders.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the patterns are extracted rather than restated.</b> A test holding its own copy of the regex proves
/// only that the copy works. These pull the literals out of <c>RatingKickoff</c> itself, so editing the
/// shipped pattern is what breaks the test — which is the whole point of having one.
/// </para>
/// <para>
/// <b>Why the cases look arbitrary.</b> They are not: they are the owner's three real locations, transcribed
/// from screenshots of their live profiles. Two of the three render the count bracketed with the words
/// "Google reviews" appearing nowhere, and for months only the third parsed — so the coverage line could
/// never name a lifetime total for the other two. A profile page that reports no total is not a cosmetic
/// gap: it downgrades "covers the first 50 of 991" to "covers 50 loaded reviews", which hides how partial
/// the scrape really is.
/// </para>
/// </remarks>
public class GoogleProfileTotalParsingTests
{
    /// <summary>
    /// The regex literals from the shipped kickoff script, in the order the script tries them.
    /// </summary>
    private static IReadOnlyList<Regex> ShippedPatterns()
    {
        // The patterns moved out of the kickoff script and into the selector manifest at A6, because they
        // are the most volatile knowledge on this channel — the same profile that once rendered the
        // bracketed layout had switched to the labelled one within days — and a manifest can be fixed
        // without shipping a binary. This still reads the SHIPPED source of truth rather than restating
        // the patterns; only its address changed.
        var manifest = SelectorManifestLoader.ForPlatform("googlebusiness");
        Assert.NotNull(manifest);

        var paired = manifest!.Anchors["reviewTotalPaired"];
        var unpaired = manifest.Anchors["reviewTotalUnpaired"];

        Assert.True(paired.IsRegex, "reviewTotalPaired must declare kind: regex.");
        Assert.True(unpaired.IsRegex, "reviewTotalUnpaired must declare kind: regex.");

        // Order matters and mirrors the script: every paired pattern (rating + total) is tried before the
        // unpaired one, because a total is only trustworthy when its own rating sits beside it.
        var found = paired.Candidates
            .Concat(unpaired.Candidates)
            .Select(p => new Regex(p, RegexOptions.IgnoreCase))
            .ToList();

        Assert.True(
            found.Count >= 3,
            $"Expected at least 3 total-parsing patterns in the googlebusiness manifest, found {found.Count}. "
            + "If the anchors were restructured, update this extraction rather than deleting the test.");

        return found;
    }

    [Fact]
    public void TheScriptStillCarriesTheseAsBuiltInFallbacks()
    {
        // The manifest is the source of truth, but the script must keep its own copy: a Google page gets no
        // adapter-core, so if the manifest fails to load there is nothing else to fall back to. This is the
        // same "manifest first, built-in second" contract as WhatsApp — asserted, because losing it would
        // be invisible until the day a manifest went missing.
        var field = typeof(GoogleReviewSnapshotService)
            .GetField("RatingKickoff", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        var script = field?.GetRawConstantValue() as string;

        Assert.False(string.IsNullOrWhiteSpace(script), "RatingKickoff is not a string const any more.");
        Assert.Contains("reviewTotalPaired", script!, StringComparison.Ordinal);
        Assert.Contains("reviewTotalUnpaired", script!, StringComparison.Ordinal);
        Assert.Contains("Google\\\\s+reviews", script!, StringComparison.Ordinal);
    }

    /// <summary>Runs the shipped chain the way the script does: first match wins.</summary>
    private static string? ParseTotal(string pageText) => ParsePage(pageText).Total;

    /// <summary>
    /// The rating the script keeps: the one captured beside the count, falling back to the aria-label.
    /// </summary>
    private static string? ParseRating(string pageText, string? ariaLabelRating) =>
        ParsePage(pageText).PairedRating ?? ariaLabelRating;

    private static (string? Total, string? PairedRating) ParsePage(string pageText)
    {
        foreach (var pattern in ShippedPatterns())
        {
            var match = pattern.Match(pageText);
            if (!match.Success)
            {
                continue;
            }

            // The count is the last capturing group in every variant — the earlier group, where there is one,
            // is the rating the pattern anchors on so the two numbers can't be sliced out of each other.
            var digits = match.Groups[match.Groups.Count - 1].Value.Replace(",", string.Empty);
            if (digits.Length == 0)
            {
                continue;
            }

            var paired = match.Groups.Count > 2 ? match.Groups[1].Value.Replace(",", ".") : null;
            return (digits, paired);
        }

        return (null, null);
    }

    [Theory]
    // The owner's three live locations, transcribed from their screenshots.
    [InlineData("991", "Depilex DHA-2 Islamabad\n4.6 ★ (991) · Beauty salon\nIslamabad")]
    [InlineData("244", "Depilex F-11 Islamabad\n4.6 ★ (244) · Beauty salon")]
    [InlineData("435", "Depilex Men\n4.7 ★★★★☆ 435 Google reviews")]
    public void EachRealProfileLayoutYieldsItsLifetimeTotal(string expected, string pageText) =>
        Assert.Equal(expected, ParseTotal(pageText));

    [Fact]
    public void TheRunTogetherLayoutStillSplitsCorrectly() =>
        // innerText renders rating and count with nothing between them. A bare ([\d,]+) here yields 6239.
        Assert.Equal("239", ParseTotal("4.6239 Google reviews"));

    [Fact]
    public void AThousandsSeparatorIsNotTruncated() =>
        Assert.Equal("1234", ParseTotal("4.8 ★ (1,234) · Salon"));

    [Fact]
    public void AnEarlierBracketedNumberIsNotMistakenForTheCount() =>
        // Opening hours render as "(closes 9 PM)" above the rating on a live profile. Anchoring the bracketed
        // pattern on the rating is what keeps this from reporting 9 reviews.
        Assert.Equal("991", ParseTotal("Open now (closes 9 PM)\n4.6 ★ (991) · Beauty salon"));

    [Fact]
    public void ALabelledCountWithNoRatingBesideItStillParses() =>
        Assert.Equal("239", ParseTotal("Reviews\n239 Google reviews"));

    // ---- which rating is believed -------------------------------------------------------------------

    [Fact]
    public void TheRatingBesideTheCountBeatsAStrayAriaLabel()
    {
        // Measured live, and the reason this section exists. The merchant view carries several
        // "Rated X out of 5" labels — individual reviews have their own, and a related-businesses panel
        // lists other branches. Taking the first one in the document reported 4.7 for the DHA-2 profile,
        // which is a DIFFERENT Depilex branch's rating, and 3.0 for the Men profile, which is one review's.
        Assert.Equal("4.6", ParseRating("4.6 ★ (991) · Beauty salon", ariaLabelRating: "4.7"));
        Assert.Equal("4.7", ParseRating("4.7 ★★★★☆ 435 Google reviews", ariaLabelRating: "3.0"));
    }

    [Fact]
    public void FiveStarGlyphsBetweenTheNumbersDoNotBreakThePairing() =>
        // Seven characters sit between "4.7" and "435" in this layout. The gap allowance was 6, so the
        // pairing silently failed and the rating fell through to the aria-label — the exact route by which
        // a 4.7 profile reported 3.0.
        Assert.Equal("4.7", ParseRating("Depilex Men\n4.7 ★★★★☆ 435 Google reviews", ariaLabelRating: null));

    [Fact]
    public void TheGapCannotStepOverAnotherNumberToPairUnrelatedFigures() =>
        // The widened run is still [^\d], so it cannot bridge across an intervening figure. Here the only
        // legitimate pairing is 4.2 with 88; a run that could cross digits would happily report 4.9.
        Assert.Equal("88", ParseTotal("4.9 ★ open\n4.2 ★ 88 Google reviews"));

    [Fact]
    public void TheAriaLabelStillFillsInWhenNoRatingSitsBesideTheCount() =>
        // The labelled-count fallback captures no rating, so the aria-label is all there is. It is a worse
        // source, not a useless one.
        Assert.Equal("4.4", ParseRating("Reviews\n239 Google reviews", ariaLabelRating: "4.4"));

    [Fact]
    public void APageWithNoReviewCountReportsNothingRatherThanGuessing() =>
        // Reporting a wrong total is worse than reporting none: the coverage line degrades honestly to
        // "covers 50 loaded reviews" when the total is unknown, but states a falsehood when it is wrong.
        Assert.Null(ParseTotal("Depilex DHA-2 Islamabad\nBeauty salon · Islamabad\nOpen now"));
}
