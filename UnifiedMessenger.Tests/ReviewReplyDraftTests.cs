using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The guardrails on an AI-drafted public reply to a Google review.
/// </summary>
/// <remarks>
/// <para>
/// The app never sends these: a draft goes to the clipboard and Google's own reply box is opened, and the
/// owner sends it themselves. That is what stops a bad model output becoming a public reply to an angry
/// one-star customer.
/// </para>
/// <para>
/// The owner reviewing it is <i>not</i> a reason to skip validation. A draft promising a refund is worse
/// than no draft even when read, because pre-written wording arrives looking approved and the commitment is
/// easy to skim past. Anything refused here is simply not offered.
/// </para>
/// </remarks>
public class ReviewReplyDraftTests
{
    private static QueuedReview Review(string text, int stars = 1, string reviewer = "Nadia Hassan") =>
        new("id", "Depilex F-11", reviewer, text, stars, "2 days ago", 0);

    // ---- refusals --------------------------------------------------------------------------------------

    [Fact]
    public void ADraftPromisingARefundIsRefused()
    {
        // The single most costly thing a drafted reply could do: commit the business to money, in public,
        // in wording the owner did not choose.
        var verdict = ReviewReplyDraft.Validate(
            "I'm so sorry about your visit. We'll process a full refund for you today.", out _);

        Assert.Equal(DraftVerdict.PromisesMoney, verdict);
    }

    [Theory]
    [InlineData("We'd like to offer you a free treatment to make up for it.")]
    [InlineData("Please accept a 20% discount on your next visit.")]
    [InlineData("We will compensate you for the inconvenience.")]
    [InlineData("We'll give you your money back.")]
    public void EveryFormOfBuyingItBackIsRefused(string draft) =>
        Assert.Equal(DraftVerdict.PromisesMoney, ReviewReplyDraft.Validate(draft, out _));

    [Fact]
    public void OfferingToPutItRightIsNotAFinancialPromise()
    {
        // This is exactly what the prompt asks for, and it must survive the guard above.
        var verdict = ReviewReplyDraft.Validate(
            "Nadia, I'm sorry you waited so long past your appointment. Please call the salon and ask for " +
            "the manager so we can put this right.", out var cleaned);

        Assert.Equal(DraftVerdict.Ok, verdict);
        Assert.StartsWith("Nadia", cleaned);
    }

    [Fact]
    public void AHalfFinishedTemplateIsRefused() =>
        Assert.Equal(
            DraftVerdict.ContainsPlaceholder,
            ReviewReplyDraft.Validate("Dear [customer name], we are sorry for the trouble.", out _));

    [Fact]
    public void AnInventedLinkOrPhoneNumberIsRefused()
    {
        Assert.Equal(DraftVerdict.ContainsLink,
            ReviewReplyDraft.Validate("Please email us at www.depilex-support.com", out _));
        Assert.Equal(DraftVerdict.ContainsLink,
            ReviewReplyDraft.Validate("Call us on 03001234567 and we'll sort it.", out _));
    }

    [Fact]
    public void AModelTalkingAboutItselfIsRefused() =>
        Assert.Equal(
            DraftVerdict.ModelTalkedAboutItself,
            ReviewReplyDraft.Validate("As an AI, I would suggest apologising to the customer.", out _));

    [Fact]
    public void AnEssayIsRefused()
    {
        // Long public replies read as defensive and nobody reads past a few lines.
        var verdict = ReviewReplyDraft.Validate(new string('a', 800), out _);
        Assert.Equal(DraftVerdict.TooLong, verdict);
    }

    [Fact]
    public void NothingAtAllIsRefused()
    {
        Assert.Equal(DraftVerdict.Empty, ReviewReplyDraft.Validate(null, out _));
        Assert.Equal(DraftVerdict.Empty, ReviewReplyDraft.Validate("   ", out _));
    }

    // ---- tidying ---------------------------------------------------------------------------------------

    [Fact]
    public void SurroundingQuotesAreStripped()
    {
        // Models habitually wrap the reply in quotes, and a reply that opens with a quote mark looks like a
        // mistake once pasted into Google.
        ReviewReplyDraft.Validate("\"Thank you so much for your kind words.\"", out var cleaned);
        Assert.Equal("Thank you so much for your kind words.", cleaned);
    }

    [Theory]
    [InlineData("Here is the reply: Thank you for visiting us.")]
    [InlineData("Reply: Thank you for visiting us.")]
    [InlineData("Here's a suggested reply: Thank you for visiting us.")]
    public void APreambleIsStrippedRatherThanRefused(string draft)
    {
        Assert.Equal(DraftVerdict.Ok, ReviewReplyDraft.Validate(draft, out var cleaned));
        Assert.Equal("Thank you for visiting us.", cleaned);
    }

    // ---- the prompt ------------------------------------------------------------------------------------

    [Fact]
    public void ThePromptCarriesOnlyWhatTheReviewActuallySays()
    {
        var prompt = ReviewReplyDraft.BuildPrompt(
            Review("I waited over an hour past my appointment.", stars: 1), "Depilex F-11");

        Assert.Contains("Depilex F-11", prompt);
        Assert.Contains("Nadia", prompt);
        Assert.Contains("1 out of 5 stars", prompt);
        Assert.Contains("waited over an hour", prompt);
    }

    [Fact]
    public void ARatingOnlyReviewSaysSoRatherThanLeavingABlank()
    {
        // A blank would invite the model to invent what they said.
        var prompt = ReviewReplyDraft.BuildPrompt(Review("", stars: 5), "Depilex F-11");
        Assert.Contains("rating with no written review", prompt);
    }

    [Fact]
    public void TheSystemPromptForbidsTheThingsTheValidatorCatches()
    {
        // Belt and braces: the model is told not to, and refused if it does anyway.
        Assert.Contains("never promise a refund", ReviewReplyDraft.SystemPrompt);
        Assert.Contains("never mention being an AI", ReviewReplyDraft.SystemPrompt);
        Assert.Contains("never include links", ReviewReplyDraft.SystemPrompt);
    }

    // ---- names -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Nadia Hassan", "Nadia")]
    [InlineData("  Ali  ", "Ali")]
    [InlineData("K.Z.A.K", "")]
    [InlineData("best music song hd", "best")]
    [InlineData("Ashfaq Mughal340", "Ashfaq")]
    [InlineData("", "")]
    [InlineData("A", "")]
    [InlineData("12345", "")]
    public void AFirstNameIsOnlyUsedWhenItLooksLikeOne(string reviewer, string expected) =>
        // Opening a public reply with the wrong name is worse than opening with none.
        Assert.Equal(expected, ReviewReplyDraft.FirstName(reviewer));
}
