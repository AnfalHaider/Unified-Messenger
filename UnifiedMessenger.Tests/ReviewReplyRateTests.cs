using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-09 — the Google review reply rate must not claim 100% while reviews are unanswered.
///
/// The seventh instance of the rounding defect first fixed in v4.99.9. The review panel shows this
/// percentage in the same subtitle that a separate branch highlights unanswered reviews, so a rounded-up
/// 100% sits beside a visible "needs a reply" list.
///
/// Note the zero-review case must NOT go through the shared helper unmodified: MetricMath.HonestPercent
/// returns 100 for a zero total (correct for "nothing outstanding"), which here would read as
/// "100% replied" for a business with no reviews at all. The Total > 0 guard is load-bearing.
/// </summary>
public class ReviewReplyRateTests
{
    private static GoogleReviewSnapshotService.ReviewHealth Health(int answered, int unanswered) =>
        new(unanswered, answered, DateTimeOffset.UtcNow, HasData: true, Pending: []);

    [Fact]
    public void OneUnansweredReviewKeepsTheRateBelowOneHundred()
    {
        // 996 of 1000 answered is 99.6%, which rounds to 100 — beside a panel listing 4 needing a reply.
        var health = Health(answered: 996, unanswered: 4);

        Assert.Equal(1000, health.Total);
        Assert.True(
            health.ReplyRatePercent < 100,
            $"4 reviews are unanswered but the panel reads {health.ReplyRatePercent}% replied");
    }

    [Fact]
    public void ASingleUnansweredReviewAmongManyIsNotRoundedAway()
    {
        var health = Health(answered: 499, unanswered: 1);

        Assert.True(
            health.ReplyRatePercent < 100,
            $"1 review is unanswered but the panel reads {health.ReplyRatePercent}% replied");
    }

    [Fact]
    public void EveryReviewAnsweredStillReportsOneHundred()
    {
        Assert.Equal(100, Health(answered: 40, unanswered: 0).ReplyRatePercent);
    }

    [Fact]
    public void ASingleAnsweredReviewAmongManyIsNotRoundedDownToZero()
    {
        var health = Health(answered: 1, unanswered: 999);

        Assert.True(
            health.ReplyRatePercent >= 1,
            $"one review was answered but the panel reads {health.ReplyRatePercent}% replied");
    }

    [Fact]
    public void NoAnsweredReviewsIsStillZero()
    {
        Assert.Equal(0, Health(answered: 0, unanswered: 12).ReplyRatePercent);
    }

    [Fact]
    public void NoReviewsAtAllReportsZeroNotOneHundred()
    {
        // THE guard that stops the shared helper being applied naively: a business with no reviews has not
        // "replied to 100% of them". The panel pairs this with "(0 on this page)" for context.
        var health = Health(answered: 0, unanswered: 0);

        Assert.Equal(0, health.Total);
        Assert.Equal(0, health.ReplyRatePercent);
    }
}
