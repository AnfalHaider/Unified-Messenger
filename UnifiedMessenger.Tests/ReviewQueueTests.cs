using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The Review Desk's ordering — which review the owner is pointed at first.
/// </summary>
/// <remarks>
/// Reviews were only ever shown grouped under the account they came from, so an owner with three salons
/// had three separate lists and no way to see that the angriest unanswered review in the business was the
/// one-star at the bottom of the second one. The ordering IS the feature; the list is just how it shows.
/// </remarks>
public class ReviewQueueTests
{
    private static GoogleReviewSnapshotService.ReviewHealth Health(
        params GoogleReviewSnapshotService.PendingReview[] pending) =>
        new(pending.Length, 0, DateTimeOffset.UtcNow, true, pending);

    private static GoogleReviewSnapshotService.PendingReview Review(
        string reviewer, int stars, string age, int index = 0, string text = "") =>
        new(reviewer, text, stars, age, index);

    private static IReadOnlyList<QueuedReview> Build(
        params (string Id, string Name, GoogleReviewSnapshotService.ReviewHealth? Health)[] accounts) =>
        ReviewQueue.Build(accounts.Select(a => (a.Id, a.Name, a.Health)));

    [Fact]
    public void TheAngriestReviewComesFirstEvenFromAnotherLocation()
    {
        // The case this whole surface exists for: per-account lists hid it.
        var queue = Build(
            ("a", "F-11", Health(Review("Happy", 5, "2 days ago"))),
            ("b", "DHA-2", Health(Review("Furious", 1, "3 weeks ago"))));

        Assert.Equal("Furious", queue[0].Reviewer);
        Assert.Equal("DHA-2", queue[0].AccountName);
    }

    [Fact]
    public void WithinTheSameRatingTheLongestWaitComesFirst()
    {
        var queue = Build(("a", "F-11", Health(
            Review("Recent", 1, "2 days ago", index: 1),
            Review("Ancient", 1, "3 weeks ago", index: 2))));

        Assert.Equal(new[] { "Ancient", "Recent" }, queue.Select(r => r.Reviewer));
    }

    [Fact]
    public void RatingBeatsAge()
    {
        // A month-old five-star is pleasant. A one-star from yesterday is costing money.
        var queue = Build(("a", "F-11", Health(
            Review("OldPraise", 5, "2 months ago", index: 1),
            Review("FreshAnger", 1, "1 day ago", index: 2))));

        Assert.Equal("FreshAnger", queue[0].Reviewer);
    }

    [Theory]
    [InlineData(1, ReviewUrgency.Critical)]
    [InlineData(2, ReviewUrgency.Critical)]
    [InlineData(3, ReviewUrgency.Elevated)]
    [InlineData(4, ReviewUrgency.Routine)]
    [InlineData(5, ReviewUrgency.Routine)]
    [InlineData(0, ReviewUrgency.Unrated)]
    [InlineData(9, ReviewUrgency.Unrated)]
    public void StarsMapToUrgency(int stars, ReviewUrgency expected) =>
        Assert.Equal(expected, ReviewQueue.UrgencyOf(stars));

    [Fact]
    public void AnUnreadRatingSitsBelowKnownComplaintsAndAboveKnownPraise()
    {
        // Most reviews are 4-5 stars, so treating an unread one as critical would bury real complaints.
        // But it might be a one-star, so it does not belong under reviews we know are positive.
        var queue = Build(("a", "F-11", Health(
            Review("Praise", 5, "1 day ago", index: 1),
            Review("Unknown", 0, "1 day ago", index: 2),
            Review("Complaint", 3, "1 day ago", index: 3))));

        Assert.Equal(new[] { "Complaint", "Unknown", "Praise" }, queue.Select(r => r.Reviewer));
    }

    [Fact]
    public void AnUnparsedAgeDoesNotJumpTheQueue()
    {
        // ReviewAge yields TimeSpan.MinValue for an age it cannot read. Sorting longest-wait-first must not
        // let that land at the top, which is what a naive null-as-zero would do in a descending sort.
        var queue = Build(("a", "F-11", Health(
            Review("Unparseable", 1, "some time back", index: 1),
            Review("Known", 1, "5 days ago", index: 2))));

        Assert.Equal(new[] { "Known", "Unparseable" }, queue.Select(r => r.Reviewer));
    }

    [Fact]
    public void AnAccountWithNoDataIsSkippedRatherThanReadAsEmpty()
    {
        // A location the scrape has not managed to read is not a location with nothing waiting, and the
        // queue must never let the first look like the second.
        var queue = Build(
            ("a", "Unread", null),
            ("b", "F-11", Health(Review("Someone", 2, "1 day ago"))));

        Assert.Single(queue);
        Assert.Equal("F-11", queue[0].AccountName);
    }

    [Fact]
    public void TheOrderIsStableAcrossRepeatedBuilds()
    {
        // The desk re-renders on every refresh; rows that swap places under a cursor are how you reply to
        // the wrong review.
        var accounts = new[]
        {
            ("a", "F-11", (GoogleReviewSnapshotService.ReviewHealth?)Health(
                Review("One", 4, "1 day ago", index: 1),
                Review("Two", 4, "1 day ago", index: 2))),
            ("b", "DHA-2", Health(Review("Three", 4, "1 day ago", index: 1)))
        };

        var first = ReviewQueue.Build(accounts.Select(a => (a.Item1, a.Item2, a.Item3)));
        var second = ReviewQueue.Build(accounts.Select(a => (a.Item1, a.Item2, a.Item3)));

        Assert.Equal(first.Select(r => r.Reviewer), second.Select(r => r.Reviewer));
    }

    // ---- the header line -----------------------------------------------------------------------------

    [Fact]
    public void AnEmptyQueueThatWasActuallyReadSaysNothingIsWaiting() =>
        Assert.Equal("Nothing waiting for a reply.", ReviewQueue.Summarise([], anyAccountRead: true));

    [Fact]
    public void AnEmptyQueueThatWasNeverReadDoesNotClaimTheWorkIsDone()
    {
        // Caught on screen: the header read "Nothing waiting for a reply" directly above a line saying
        // reviews had not been read yet. A scrape that silently stops working would otherwise report itself
        // as a clean queue forever — the worst failure this surface can have, because it looks like success.
        Assert.Equal(
            "Not read yet — the app checks in the background.",
            ReviewQueue.Summarise([], anyAccountRead: false));
    }

    [Fact]
    public void TheSummaryLeadsWithWhatChangesBehaviour()
    {
        // "12 waiting" does not tell an owner whether to deal with it now. "3 from unhappy customers" does.
        var queue = Build(("a", "F-11", Health(
            Review("A", 1, "1 day ago", index: 1),
            Review("B", 5, "1 day ago", index: 2))));

        Assert.Equal(
            "2 reviews waiting · 1 from unhappy customers",
            ReviewQueue.Summarise(queue, anyAccountRead: true));
    }

    [Fact]
    public void NoUnhappyReviewsMeansNoAlarmingClause()
    {
        var queue = Build(("a", "F-11", Health(Review("A", 5, "1 day ago"))));
        Assert.Equal("1 review waiting", ReviewQueue.Summarise(queue, anyAccountRead: true));
    }
}
