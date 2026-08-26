using UnifiedMessenger.Controls;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The Reviews desk showed the reply QUEUE's length under the label "Unanswered".
/// </summary>
/// <remarks>
/// The scrape reads the reply-button count for the whole page but builds preview text for only the first
/// eight, and pagination is capped at one page — so the queue holds at most 8 while
/// <c>ReviewHealth.Unanswered</c> holds the real figure. The sidebar badge has always used the real one,
/// so a business with 45 waiting reviews saw a badge reading 45 and a page reading 8, with a reply-rate
/// basis line ("5 of 50 read") implying the badge was right. Nothing on the page said the queue was a
/// sample.
/// </remarks>
public class ReviewQueueSampleTests
{
    [Theory]
    [InlineData(8, 45, true)]
    [InlineData(8, 9, true)]
    [InlineData(8, 8, false)]
    [InlineData(8, 3, false)]   // scrape saw fewer than the queue holds — not a sample
    [InlineData(0, 0, false)]
    public void QueueIsSample_IsTrueOnlyWhenSomeUnansweredReviewIsMissingFromTheQueue(
        int shown, int unanswered, bool expected) =>
        Assert.Equal(expected, ReviewCoverage.QueueIsSample(shown, unanswered));

    [Fact]
    public void DescribeQueueSample_NamesTheSetWhenTheQueueIsPartial() =>
        Assert.Equal("in the 8 loaded", ReviewCoverage.DescribeQueueSample(8, 45));

    [Fact]
    public void DescribeQueueSample_SaysNothingWhenTheQueueIsEverything() =>
        Assert.Equal(string.Empty, ReviewCoverage.DescribeQueueSample(8, 8));

    [Fact]
    public void DescribeQueueSample_ReadsAsEnglishForASingleReview() =>
        Assert.Equal("in the 1 loaded", ReviewCoverage.DescribeQueueSample(1, 6));

    [Fact]
    public void LowStarSub_QualifiesTheCountWhenItWasTakenOverASample() =>
        Assert.Equal("3 at 3 stars or below in the 8 loaded", ReviewDesk.LowStarSub(3, 8, 45));

    [Fact]
    public void LowStarSub_StatesItPlainlyWhenTheQueueIsComplete() =>
        Assert.Equal("3 at 3 stars or below", ReviewDesk.LowStarSub(3, 8, 8));

    [Fact]
    public void LowStarSub_HandlesTheGoodCase() =>
        Assert.Equal("none at 3 stars or below", ReviewDesk.LowStarSub(0, 8, 8));

    [Fact]
    public void LowStarSub_StillQualifiesZeroWhenItOnlyLookedAtASample()
    {
        // "none at 3 stars or below" over 8 of 45 reviews is the reassuring-because-data-is-missing
        // failure this audit kept finding. It has to name what it looked at.
        var sub = ReviewDesk.LowStarSub(0, 8, 45);

        Assert.Equal("none at 3 stars or below in the 8 loaded", sub);
    }
}
