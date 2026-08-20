using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Google paginates the reviews manager, so the scrape's totals are "reviews on the page we loaded" — but
/// the panel rendered them bare, where they read as "reviews this business has".
///
/// <para>
/// A salon with 239 reviews being shown "40 · 88% replied" is being told something false about itself, and
/// the reply rate is over the loaded window rather than the profile. This is the type that makes the
/// difference visible instead of leaving it in a code comment.
/// </para>
/// </summary>
public class ReviewCoverageTests
{
    [Fact]
    public void SeeingEveryReviewSaysSo() =>
        Assert.Equal("covers all 239 reviews", ReviewCoverage.Describe(239, 239));

    [Fact]
    public void SeeingPartOfThemSaysHowMuch() =>
        Assert.Equal("covers the 40 most recent of 239", ReviewCoverage.Describe(40, 239));

    [Fact]
    public void AnUnknownLifetimeTotalNeverClaimsCompleteness()
    {
        // The lifetime total comes from a separate, six-hourly scrape and may not have run. Saying
        // "covers all" on the strength of a number never read is the confident wrongness this tier removes.
        Assert.Equal("covers 40 loaded reviews", ReviewCoverage.Describe(40, null));
        Assert.False(ReviewCoverage.IsComplete(40, null));
    }

    [Fact]
    public void AStaleLifetimeTotalBelowTheLoadedCountDoesNotRenderAsABug()
    {
        // The two scrapes are throttled independently, so the cached lifetime total can lag behind reviews
        // that have just arrived. "covers 41 of 39" would read as broken arithmetic.
        Assert.Equal("covers all 39 reviews", ReviewCoverage.Describe(41, 39));
    }

    [Theory]
    [InlineData(0, 239, "no reviews read yet of 239")]
    [InlineData(0, null, "no reviews read yet")]
    public void NothingReadIsStatedPlainly(int loaded, int? total, string expected) =>
        Assert.Equal(expected, ReviewCoverage.Describe(loaded, total));

    [Fact]
    public void OneReviewIsNotPluralised() =>
        Assert.Equal("covers 1 loaded review", ReviewCoverage.Describe(1, null));

    [Fact]
    public void LargeNumbersAreGrouped() =>
        Assert.Equal("covers the 40 most recent of 1,240", ReviewCoverage.Describe(40, 1240));

    // ---- what the reply rate is a rate OF ----------------------------------------------------------

    [Fact]
    public void AProfileWideReplyRateSaysSo() =>
        Assert.Equal("of all reviews", ReviewCoverage.DescribeReplyRateBasis(239, 239));

    [Fact]
    public void APartialReplyRateNamesItsWindow() =>
        Assert.Equal("of the 40 most recent", ReviewCoverage.DescribeReplyRateBasis(40, 239));

    [Fact]
    public void CompletenessRequiresAKnownTotal()
    {
        Assert.True(ReviewCoverage.IsComplete(239, 239));
        Assert.True(ReviewCoverage.IsComplete(240, 239));
        Assert.False(ReviewCoverage.IsComplete(40, 239));
        Assert.False(ReviewCoverage.IsComplete(40, 0));
    }

    // ---- coverage stated from the traversal, not inferred -------------------------------------------

    [Fact]
    public void ReachingTheLastPageMeansTheLoadedCountIsTheProfile()
    {
        // The traversal clicked Next until it was disabled. That is a fact about this scrape and beats any
        // inference from the separately-scraped lifetime total.
        Assert.Equal("covers all 239 reviews", ReviewCoverage.Describe(239, 239, reachedLastPage: true));
        Assert.Equal("of all reviews", ReviewCoverage.DescribeReplyRateBasis(239, 239, reachedLastPage: true));
    }

    [Fact]
    public void ReachingTheEndWinsOverAStaleLifetimeTotal()
    {
        // The lifetime total is refreshed every six hours and can lag reviews that just arrived. If we
        // walked to the final page, we counted them — the cached total is the number that is wrong.
        Assert.Equal("covers all 244 reviews", ReviewCoverage.Describe(244, 239, reachedLastPage: true));
    }

    [Fact]
    public void StoppingEarlyNamesWhereItStopped()
    {
        // "the first 100 of 239" tells the owner the reply rate below is over recent reviews only.
        Assert.Equal("covers the first 100 of 239", ReviewCoverage.Describe(100, 239, reachedLastPage: false));
        Assert.Equal("of the 100 most recent", ReviewCoverage.DescribeReplyRateBasis(100, 239, reachedLastPage: false));
    }

    [Fact]
    public void StoppingEarlyWithNoKnownTotalStillDoesNotClaimCompleteness() =>
        Assert.Equal("covers 100 loaded reviews", ReviewCoverage.Describe(100, null, reachedLastPage: false));

    [Fact]
    public void ReachingTheEndOfAnEmptyProfileIsNotACompletenessClaim()
    {
        // Zero reviews and a disabled Next is a profile with no reviews, not "covers all 0 reviews".
        Assert.Equal("no reviews read yet", ReviewCoverage.Describe(0, null, reachedLastPage: true));
    }
}
