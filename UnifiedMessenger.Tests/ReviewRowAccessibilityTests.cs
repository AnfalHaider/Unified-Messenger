using UnifiedMessenger.Controls;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// What a screen reader says for one review row (Increment 131).
///
/// <para>
/// Every review row is a <c>Button</c> whose content is a panel, so it carried no accessible name: Narrator
/// announced "button" for each one, with no way to tell a one-star complaint from a five-star thank-you.
/// The identical defect was fixed for the needs-reply rows earlier and never applied here — found by
/// scanning code-built buttons for missing names <i>before</i> asking the owner to sit through a listening
/// session, rather than spending their time on it.
/// </para>
/// </summary>
public class ReviewRowAccessibilityTests
{
    private static QueuedReview Review(
        int stars = 1,
        string reviewer = "Sana Tariq",
        string text = "Waited 40 minutes past my appointment.",
        string account = "Depilex DHA-2") =>
        new(
            InstanceId: "g1",
            AccountName: account,
            Reviewer: reviewer,
            Text: text,
            Stars: stars,
            Age: "2 days",
            Index: 0);

    [Fact]
    public void TheNameLeadsWithTheStarRating()
    {
        var name = ReviewDesk.BuildReviewRowName(Review(stars: 1));

        // The rating decides whether the row needs opening at all — and it is the one fact assistive
        // technology cannot recover on its own, because Google carries it in the star glyphs' COLOUR
        // rather than their codepoints. All five stars are the same character.
        Assert.StartsWith("1 star from", name, StringComparison.Ordinal);
    }

    [Fact]
    public void PluralisationIsCorrectAtEveryRating()
    {
        Assert.Contains("1 star ", ReviewDesk.BuildReviewRowName(Review(stars: 1)), StringComparison.Ordinal);
        Assert.Contains("5 stars ", ReviewDesk.BuildReviewRowName(Review(stars: 5)), StringComparison.Ordinal);
    }

    [Fact]
    public void TheNameCarriesWhoWhereAndWhen()
    {
        var name = ReviewDesk.BuildReviewRowName(Review());

        Assert.Contains("Sana Tariq", name, StringComparison.Ordinal);
        Assert.Contains("Depilex DHA-2", name, StringComparison.Ordinal);
        Assert.Contains("ago", name, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReviewTextIsSpokenRatherThanLeftToSight()
    {
        var name = ReviewDesk.BuildReviewRowName(Review());

        Assert.Contains("Waited 40 minutes", name, StringComparison.Ordinal);
    }

    [Fact]
    public void ARatingOnlyReviewSaysSoRatherThanFallingSilent()
    {
        var name = ReviewDesk.BuildReviewRowName(Review(text: ""));

        // An empty body would make the row announce a rating and then stop, which sounds like a truncated
        // read rather than a review with nothing written in it.
        Assert.Contains("Rating only", name, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnonymousReviewerStillGetsAReadableName()
    {
        var name = ReviewDesk.BuildReviewRowName(Review(reviewer: ""));

        Assert.Contains("A reviewer", name, StringComparison.Ordinal);
        Assert.DoesNotContain("from  at", name, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingRatingIsNamedRatherThanImplied()
    {
        var name = ReviewDesk.BuildReviewRowName(Review(stars: 0));

        Assert.StartsWith("No rating", name, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNameSaysWhatActivatingTheRowDoes()
    {
        // A row that names its content but not its action leaves a keyboard user guessing whether Enter
        // opens it, dismisses it, or replies.
        Assert.Contains("Activate to open and reply", ReviewDesk.BuildReviewRowName(Review()), StringComparison.Ordinal);
    }
}
