using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// When an unhappy review is allowed to interrupt the owner.
/// </summary>
/// <remarks>
/// The failure mode this is built around is over-notifying. A missed toast costs a few minutes — the review
/// is still at the top of the desk. A wrong toast trains the owner to dismiss them, and after that the
/// feature is worse than absent. Every case below is about erring towards silence.
/// </remarks>
public class ReviewAlertsTests
{
    private static QueuedReview R(string reviewer, int stars, string age = "2 days ago", string account = "DHA-2") =>
        new("acc-1", account, reviewer, "text", stars, age, 0);

    private static HashSet<string> Empty() => new(StringComparer.Ordinal);

    [Fact]
    public void TheFirstEverLookAlertsOnNothing()
    {
        // Installing on a salon with five unanswered one-stars must not fire five notifications about
        // reviews that are weeks old.
        var (toAlert, seen) = ReviewAlerts.Evaluate(
            [R("Angry One", 1), R("Angry Two", 1), R("Cross", 2)], Empty(), seeded: false);

        Assert.Empty(toAlert);
        Assert.Equal(3, seen.Count);   // recorded silently, so none of them alerts later either
    }

    [Fact]
    public void AReviewThatArrivesAfterSeedingDoesAlert()
    {
        var (_, seen) = ReviewAlerts.Evaluate([R("Old Anger", 1)], Empty(), seeded: false);

        var (toAlert, _) = ReviewAlerts.Evaluate(
            [R("Old Anger", 1), R("New Anger", 1)], new HashSet<string>(seen, StringComparer.Ordinal), seeded: true);

        var only = Assert.Single(toAlert);
        Assert.Equal("New Anger", only.Reviewer);
    }

    [Fact]
    public void TheSameReviewNeverAlertsTwice()
    {
        var seen = Empty();
        var (first, updated) = ReviewAlerts.Evaluate([R("Angry", 1)], seen, seeded: true);
        Assert.Single(first);

        var (second, _) = ReviewAlerts.Evaluate(
            [R("Angry", 1)], new HashSet<string>(updated, StringComparer.Ordinal), seeded: true);
        Assert.Empty(second);
    }

    [Fact]
    public void AgeingDoesNotMakeAReviewNewAgain()
    {
        // The scraped age changes every day. Keying on it would re-alert every review every morning.
        var (_, seen) = ReviewAlerts.Evaluate([R("Angry", 1, "2 days ago")], Empty(), seeded: true);

        var (again, _) = ReviewAlerts.Evaluate(
            [R("Angry", 1, "3 days ago")], new HashSet<string>(seen, StringComparer.Ordinal), seeded: true);

        Assert.Empty(again);
    }

    [Fact]
    public void AFailedScrapeFollowedByAGoodOneDoesNotRefireEverything()
    {
        // The bug that "prune keys no longer present" would introduce: one empty read, then a burst of
        // toasts about reviews the owner saw last week.
        var (_, afterFirst) = ReviewAlerts.Evaluate([R("Angry", 1), R("Cross", 2)], Empty(), seeded: true);

        // A failed pass returns nothing at all.
        var (duringFailure, afterFailure) = ReviewAlerts.Evaluate(
            [], new HashSet<string>(afterFirst, StringComparer.Ordinal), seeded: true);
        Assert.Empty(duringFailure);

        // And the recovery must be silent.
        var (afterRecovery, _) = ReviewAlerts.Evaluate(
            [R("Angry", 1), R("Cross", 2)], new HashSet<string>(afterFailure, StringComparer.Ordinal), seeded: true);
        Assert.Empty(afterRecovery);
    }

    [Fact]
    public void OnlyUnhappyReviewsInterrupt()
    {
        // A five-star review is good news and can wait for the owner to open the page.
        var (toAlert, _) = ReviewAlerts.Evaluate(
            [R("Delighted", 5), R("Pleased", 4), R("Fine", 3)], Empty(), seeded: true);

        Assert.Empty(toAlert);
    }

    [Fact]
    public void TheWorstAndOldestComesFirst()
    {
        var (toAlert, _) = ReviewAlerts.Evaluate(
            [R("Two Star", 2), R("One Star Recent", 1, "1 day ago"), R("One Star Old", 1, "3 weeks ago")],
            Empty(), seeded: true);

        Assert.Equal(["One Star Old", "One Star Recent", "Two Star"], toAlert.Select(r => r.Reviewer));
    }

    // ---- the message ------------------------------------------------------------------------------------

    [Fact]
    public void NothingNewMeansNoToastAtAll() =>
        Assert.Null(ReviewAlerts.BuildToast([]));

    [Fact]
    public void OneReviewNamesTheCustomerAndBranch()
    {
        var toast = ReviewAlerts.BuildToast([R("Nadia Hassan", 1, account: "Depilex F-11")])!.Value;

        Assert.Equal("New one-star review", toast.Title);
        Assert.Contains("Nadia Hassan", toast.Body);
        Assert.Contains("Depilex F-11", toast.Body);
    }

    [Fact]
    public void SeveralReviewsProduceOneToastNotSeveral()
    {
        // Three toasts for three reviews is how a useful signal becomes noise the owner switches off.
        var toast = ReviewAlerts.BuildToast([R("A", 1), R("B", 1), R("C", 2)])!.Value;

        Assert.Equal("3 new unhappy reviews", toast.Title);
        Assert.Contains("Worst is A", toast.Body);
    }
}

/// <summary>
/// The sidebar badge means two different things depending on the account it sits on.
/// </summary>
public class SidebarReviewBadgeWordingTests
{
    [Fact]
    public void AGoogleAccountsBadgeIsAnnouncedAsReviewsNotUnreadMessages()
    {
        // Google Business has no messaging at all — Business Messages shut down in 2024 — so "6 unread"
        // is simply false to anyone who cannot see which kind of account the row is.
        var name = WorkspaceSidebarAccessibility.ComposeInstanceName(
            "Google Depilex DHA-2", "Google Business", 6, selected: false, badgeCountsReviews: true);

        Assert.Contains("6 reviews awaiting a reply", name);
        Assert.DoesNotContain("unread", name);
    }

    [Fact]
    public void OneReviewIsNotPluralised() =>
        Assert.Contains(
            "1 review awaiting a reply",
            WorkspaceSidebarAccessibility.ComposeInstanceName(
                "Google Depilex F-11", "Google Business", 1, selected: false, badgeCountsReviews: true));

    [Fact]
    public void AMessagingAccountStillSaysUnread() =>
        // The wording only changes where the meaning does.
        Assert.Contains(
            "4 unread",
            WorkspaceSidebarAccessibility.ComposeInstanceName(
                "Depilex DHA-2 WhatsApp", "WhatsApp", 4, selected: false));
}
