using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The trend maths behind the Review Desk's history tiles.
/// </summary>
/// <remarks>
/// <para>
/// The load-bearing idea: review <i>velocity</i> is derived from the profile's lifetime total, not from
/// per-review dates the scrape never sees. The difference between two readings of that total is exactly how
/// many reviews arrived, answered or not.
/// </para>
/// <para>
/// The load-bearing constraint: every figure is reported with the span it was actually measured over. With
/// four days of history, "gained in the last 30 days" is not a statement anyone can make.
/// </para>
/// </remarks>
public class ReviewTrendTests
{
    private static DateOnly D(int day) => new(2026, 8, day);

    private static ReviewDayPoint P(int day, double? rating = null, int? total = null, int unanswered = 0, int answered = 0) =>
        new(D(day), rating, total, unanswered, answered);

    // ---- rating ---------------------------------------------------------------------------------------

    [Fact]
    public void ARatingChangeNeedsTwoDifferentDays()
    {
        // One reading is not a trend. Reporting "no change" from a single point is a claim about stability
        // that one measurement cannot support.
        Assert.Null(ReviewTrend.RatingChange([P(1, rating: 4.6)], 30));
        Assert.Null(ReviewTrend.RatingChange([], 30));
    }

    [Fact]
    public void ARatingChangeReportsItsEndpointsAndSpan()
    {
        var change = ReviewTrend.RatingChange([P(1, rating: 4.4), P(11, rating: 4.6)], 30);

        Assert.NotNull(change);
        Assert.Equal(4.4, change!.Value.From);
        Assert.Equal(4.6, change.Value.To);
        Assert.Equal(10, change.Value.OverDays);
    }

    [Fact]
    public void DaysWithNoRatingAreIgnoredRatherThanTreatedAsZero()
    {
        // The rating comes from a separate six-hourly scrape that can fail on its own. A failed day must not
        // enter the series as 0.0, which would render as a catastrophic drop.
        var change = ReviewTrend.RatingChange([P(1, rating: 4.4), P(2), P(3, rating: 4.5)], 30);

        Assert.NotNull(change);
        Assert.Equal(4.4, change!.Value.From);
        Assert.Equal(4.5, change.Value.To);
    }

    // ---- velocity from the lifetime total --------------------------------------------------------------

    [Fact]
    public void ReviewsGainedIsTheLifetimeTotalsIncrease()
    {
        var gained = ReviewTrend.ReviewsGained([P(1, total: 985), P(31, total: 992)], 30);

        Assert.NotNull(gained);
        Assert.Equal(985, gained!.Value.From);
        Assert.Equal(992, gained.Value.To);
        Assert.Equal(30, gained.Value.OverDays);
    }

    [Fact]
    public void AShortHistoryReportsItsOwnSpanNotTheRequestedOne()
    {
        // Asked for 30 days, holds 4. The answer is "+2 in 4 days" — never "+2 this month".
        var gained = ReviewTrend.ReviewsGained([P(18, total: 990), P(22, total: 992)], 30);

        Assert.NotNull(gained);
        Assert.Equal(4, gained!.Value.OverDays);
        Assert.Equal(2, gained.Value.To - gained.Value.From);
    }

    [Fact]
    public void ReadingsOlderThanTheWindowAreExcluded()
    {
        var gained = ReviewTrend.ReviewsGained([P(1, total: 900), P(20, total: 990), P(25, total: 992)], 7);

        Assert.NotNull(gained);
        Assert.Equal(990, gained!.Value.From);
        Assert.Equal(5, gained.Value.OverDays);
    }

    // ---- quiet branches -------------------------------------------------------------------------------

    [Fact]
    public void DaysSinceNewReviewMeasuresFromTheLastIncrease()
    {
        var points = new[] { P(1, total: 990), P(5, total: 992), P(10, total: 992) };
        Assert.Equal(15, ReviewTrend.DaysSinceNewReview(points, D(20)));
    }

    [Fact]
    public void NoIncreaseAnywhereMeasuresFromTheOldestReadingWeHold()
    {
        // "At least this long" is honest; inventing a start date before our first reading is not.
        var points = new[] { P(4, total: 992), P(9, total: 992) };
        Assert.Equal(16, ReviewTrend.DaysSinceNewReview(points, D(20)));
    }

    [Fact]
    public void QuietnessIsUnknowableFromASingleReading() =>
        Assert.Null(ReviewTrend.DaysSinceNewReview([P(1, total: 992)], D(20)));

    // ---- reply rate -----------------------------------------------------------------------------------

    [Fact]
    public void ReplyRateChangeComesFromTheAnsweredShare()
    {
        var change = ReviewTrend.ReplyRateChange(
            [P(1, unanswered: 20, answered: 80), P(11, unanswered: 6, answered: 94)], 30);

        Assert.NotNull(change);
        Assert.Equal(80, change!.Value.From);
        Assert.Equal(94, change.Value.To);
    }

    [Fact]
    public void ADayWithNoReviewsReadIsNotAZeroPercentReplyRate()
    {
        // A failed scrape records 0 unanswered and 0 answered. Treating that as 0% would show a collapse.
        var change = ReviewTrend.ReplyRateChange(
            [P(1, unanswered: 10, answered: 90), P(2), P(3, unanswered: 5, answered: 95)], 30);

        Assert.NotNull(change);
        Assert.Equal(90, change!.Value.From);
        Assert.Equal(95, change.Value.To);
    }

    // ---- how the span is worded ------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "today")]
    [InlineData(1, "since yesterday")]
    [InlineData(4, "in 4 days")]
    [InlineData(30, "in 4 weeks")]
    [InlineData(90, "in 3 months")]
    public void TheSpanIsAlwaysStated(int days, string expected) =>
        Assert.Equal(expected, ReviewTrend.SpanLabel(days));

    [Fact]
    public void TheSparklineSkipsDaysWithNoRating() =>
        Assert.Equal(
            new[] { 4.4, 4.6 },
            ReviewTrend.RatingSeries([P(1, rating: 4.4), P(2), P(3, rating: 4.6)], 30));
}
