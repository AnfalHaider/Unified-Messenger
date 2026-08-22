using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// What the waiting reviews keep saying — computed here so no model ever has to count.
/// </summary>
/// <remarks>
/// The insight line is only worth showing if "three of them mention waiting time" is true. A language model
/// asked to both find and count themes produces a fluent number, and nobody reading a dashboard can tell a
/// counted three from an invented one. These pin the arithmetic; the model only phrases it.
/// </remarks>
public class ReviewThemesTests
{
    private static QueuedReview R(string text, string branch = "DHA-2", int stars = 1) =>
        new("id", branch, "Someone", text, stars, "2 days ago", 0);

    [Fact]
    public void ASubjectMentionedOnceIsNotATheme()
    {
        // One customer mentioning parking is an anecdote. Separating recurring from one-off is the whole
        // point of the line, so a threshold of one would defeat it.
        var themes = ReviewThemes.Extract([R("parking was a nightmare")]);
        Assert.Empty(themes);
    }

    [Fact]
    public void ASubjectMentionedTwiceIsATheme()
    {
        var themes = ReviewThemes.Extract([
            R("I waited over an hour past my appointment"),
            R("waited 40 minutes with a booking already made")
        ]);

        var waiting = Assert.Single(themes, t => t.Label == "waiting time");
        Assert.Equal(2, waiting.Count);
        Assert.True(waiting.IsComplaint);
    }

    [Fact]
    public void AReviewCanBelongToMoreThanOneTheme()
    {
        // "Waited an hour and the staff were rude" is genuinely both. Forcing one label would undercount
        // whichever lost — which is why the counts are per theme and never presented as a share.
        var themes = ReviewThemes.Extract([
            R("waited an hour and the staff were rude"),
            R("waited ages"),
            R("very rude receptionist")
        ]);

        Assert.Equal(2, themes.Single(t => t.Label == "waiting time").Count);
        Assert.Equal(2, themes.Single(t => t.Label == "staff attitude").Count);
    }

    [Fact]
    public void ComplaintsAreRankedAboveCompliments()
    {
        // Praise is pleasant; complaints are what the owner has to act on today.
        var themes = ReviewThemes.Extract([
            R("excellent service, highly recommend"),
            R("amazing, highly recommend"),
            R("very good, professional"),
            R("waited an hour"),
            R("waited far too long")
        ]);

        Assert.True(themes[0].IsComplaint);
        Assert.Equal("waiting time", themes[0].Label);
    }

    [Fact]
    public void TheBranchesAThemeCameFromAreRecorded()
    {
        var themes = ReviewThemes.Extract([
            R("waited an hour", branch: "F-11"),
            R("waited too long", branch: "F-11")
        ]);

        Assert.Equal(["F-11"], themes.Single(t => t.Label == "waiting time").Branches);
    }

    [Fact]
    public void ReviewsWithNoTextAreIgnored()
    {
        // Star-only reviews are common and carry no subject at all.
        var themes = ReviewThemes.Extract([R(""), R("   "), R("waited an hour"), R("waited ages")]);
        Assert.Single(themes);
    }

    // ---- the sentence ----------------------------------------------------------------------------------

    [Fact]
    public void NothingRecurringMeansNoSentenceAtAll()
    {
        // The caller hides the strip rather than showing a line that says nothing.
        Assert.Null(ReviewThemes.Describe([], 5));
        Assert.Null(ReviewThemes.Describe(ReviewThemes.Extract([R("parking")]), 1));
    }

    [Fact]
    public void TheSentenceNamesItsPopulationAndSaysTheseAreWaitingReviews()
    {
        // Scope matters: the scrape only captures text for reviews awaiting a reply, so this describes the
        // queue and not the business's reviews as a whole. The wording has to admit that.
        var themes = ReviewThemes.Extract([
            R("waited an hour", branch: "F-11"),
            R("waited too long", branch: "F-11")
        ]);

        var sentence = ReviewThemes.Describe(themes, reviewsWithText: 6);

        Assert.Equal("Two of the 6 waiting reviews with text mention waiting time, all at F-11.", sentence);
    }

    [Fact]
    public void OneBranchIsNamedButSeveralAreNot()
    {
        // "all at F-11" is actionable. Listing three branches is just the whole business again.
        var themes = ReviewThemes.Extract([
            R("waited an hour", branch: "F-11"),
            R("waited too long", branch: "DHA-2")
        ]);

        var sentence = ReviewThemes.Describe(themes, reviewsWithText: 4);
        Assert.DoesNotContain("all at", sentence);
    }

    [Fact]
    public void ASecondThemeIsMentionedButNoMore()
    {
        var themes = ReviewThemes.Extract([
            R("waited an hour"), R("waited too long"),
            R("very rude"), R("rude staff"),
            R("too expensive"), R("overcharged me")
        ]);

        var sentence = ReviewThemes.Describe(themes, reviewsWithText: 6)!;

        // Two subjects is a summary; four is just the list again in prose.
        Assert.Equal(2, sentence.Split(" mention ").Length - 1);
    }
}
