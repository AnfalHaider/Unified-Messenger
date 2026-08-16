using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The "awaiting a reply" count was a pure direction flag — the customer spoke last and nobody typed
/// since. On the owner's real data that read <b>466 customers waiting, oldest 82 days</b>, of which only
/// 41 had asked anything and 454 had already been read.
///
/// <para>
/// Every example in this file is <b>verbatim from that data</b>, including the spelling. That is
/// deliberate: a lexicon written from imagination would have been all "ok" and "thanks" and would have
/// closed almost nothing, because these customers write "oky", "ji", "g", "ok jazakallah" and
/// "walaikum us salam".
/// </para>
/// <para>
/// <b>The asymmetry is the whole design.</b> Counting a finished conversation wastes a glance. Dropping a
/// live one loses a customer. So the tests that matter most here are the ones asserting something is
/// <i>kept</i>.
/// </para>
/// </summary>
public class ReplyNeedTests
{
    // ---- Things that must ALWAYS stay in the count ---------------------------------------------------

    [Theory]
    // Real, unanswered, and the reason this feature exists at all.
    [InlineData("V v v unprofessional staff Came for waxing yesterday My girls got bruises on legs")]
    [InlineData("Warna main kahin aur chali jawon")]
    [InlineData("Thanku bhot expensive hn ap")]
    [InlineData("Aaj ka nahae pocha rahie wasie kiya charge kerain gaye")]
    [InlineData("Any chance of discount")]
    [InlineData("Mujy full body waxing karani hai")]
    [InlineData("i want to dye my hair with a group of 4-5 friends")]
    [InlineData("Ill be there around 12.30 pm")]
    [InlineData("Near chandni chok")]
    [InlineData("Both signature and senior artist")]
    public void ARealCustomerMessageIsNeverClosed(string preview)
    {
        Assert.True(ReplyNeed.Classify(preview).NeedsReply, $"'{preview}' was dropped from the count.");
    }

    [Theory]
    [InlineData("ok but what time")]
    [InlineData("Thanks, kitna charge hoga?")]
    [InlineData("okay and can you send the address")]
    [InlineData("ji kab available hain")]
    [InlineData("sure, price kya hai")]
    public void AnAcknowledgementWithAQuestionAttachedIsStillAQuestion(string preview)
    {
        // The single most dangerous failure mode: "ok" appears first, so a naive prefix or
        // any-word match would file the whole thing as an acknowledgement and lose the question.
        var verdict = ReplyNeed.Classify(preview);

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.AsksSomething, verdict.Reason);
    }

    [Theory]
    [InlineData("Hi")]
    [InlineData("Salam")]
    [InlineData("Hello")]
    [InlineData("Assalam o alaikum")]
    public void AnOpeningGreetingIsAnUnansweredLeadNotAClosedChat(string preview)
    {
        // Caught by running the classifier over real traffic rather than by reading it: a bare "Salam"
        // was being filed as a sign-off. For a salon, a greeting nobody answered is the single most
        // expensive thing to drop — it is a customer who tried to start a conversation.
        Assert.True(ReplyNeed.Classify(preview).NeedsReply, $"'{preview}' was treated as a sign-off.");
    }

    [Theory]
    [InlineData("Photo")]
    [InlineData("Voice")]
    [InlineData("photo")]
    [InlineData("Video")]
    [InlineData("Document")]
    public void AnUncaptionedPhotoIsUsuallyAQuestionWithNoWordsInIt(string preview)
    {
        // "Can you do this style?" sent as a picture. There is no text to judge, so it is kept.
        var verdict = ReplyNeed.Classify(preview);

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MediaWithoutCaption, verdict.Reason);
    }

    [Fact]
    public void AChatWhosePreviewCouldNotBeReadIsNeverJudged()
    {
        // 200 of the owner's 466 land here — the scrape had not filled in the message body yet. Closing
        // on absent evidence would silently erase almost half the queue.
        foreach (var preview in new[] { null, "", "   ", "\n" })
        {
            var verdict = ReplyNeed.Classify(preview);

            Assert.True(verdict.NeedsReply);
            Assert.Equal(ReplyNeedReason.NoPreviewAvailable, verdict.Reason);
        }
    }

    [Theory]
    [InlineData("no problem")]
    [InlineData("Ok no issue")]
    [InlineData("Ok fine no problem")]
    public void TheWordsProblemAndIssueDoNotMakeAClosingMessageAComplaint(string preview)
    {
        // These read as complaints to a naive keyword list. They are the opposite — all three are
        // verbatim sign-offs from the real data.
        Assert.False(ReplyNeed.Classify(preview).NeedsReply);
    }

    [Fact]
    public void AnUnrecognisedWordKeepsTheWholeMessage()
    {
        // The lexicon is a whitelist, not a blacklist. One word it does not know and the message stays
        // counted, which is what makes an incomplete lexicon safe rather than lossy.
        Assert.True(ReplyNeed.Classify("ok bhijwa dain").NeedsReply);
        Assert.True(ReplyNeed.Classify("thanks mel to mel").NeedsReply);
    }

    [Fact]
    public void ALongMessageIsNeverClosedNoMatterWhatWordsItUses()
    {
        var padded = string.Join(" ", Enumerable.Repeat("ok", ReplyNeed.MaxClosingWords + 1));

        Assert.True(ReplyNeed.Classify(padded).NeedsReply);
    }

    // ---- Things that should genuinely stop being counted ----------------------------------------------

    [Theory]
    [InlineData("Ok")]
    [InlineData("okay")]
    [InlineData("Oky")]
    [InlineData("Okk")]
    [InlineData("Oka")]
    [InlineData("Okie")]
    [InlineData("Ji")]
    [InlineData("G")]
    [InlineData("Gg")]
    [InlineData("Ho")]
    [InlineData("Sure")]
    [InlineData("Done")]
    [InlineData("Np")]
    [InlineData("Its ok")]
    [InlineData("Ok ok")]
    [InlineData("Ok miss")]
    [InlineData("Ok Thx")]
    [InlineData("Ok thanks")]
    [InlineData("Ok thank you")]
    [InlineData("Okay tysm")]
    [InlineData("Ok JazakAllah")]
    [InlineData("Thankyou so much")]
    [InlineData("Thanku so much")]
    [InlineData("Your welcome")]
    [InlineData("Thank you dear")]
    [InlineData("okay, thank you")]
    public void AConversationCloserStopsBeingCounted(string preview)
    {
        var verdict = ReplyNeed.Classify(preview);

        Assert.False(verdict.NeedsReply, $"'{preview}' is still counted as awaiting a reply.");
        Assert.Equal(ReplyNeedReason.Acknowledgement, verdict.Reason);
    }

    [Fact]
    public void AReciprocalGreetingClosesButAnOpeningOneDoesNot()
    {
        // "Walaikum" means "and upon you" — it can only ever be an answer, never an opener. That single
        // word is what separates the two, and matching on "salam" alone conflated them.
        Assert.False(ReplyNeed.Classify("Walaikum us salam").NeedsReply);
        Assert.False(ReplyNeed.Classify("walaikum salam").NeedsReply);

        Assert.True(ReplyNeed.Classify("Salam").NeedsReply);
    }

    [Theory]
    [InlineData("👍")]
    [InlineData("😍")]
    [InlineData("☺️")]
    [InlineData("👍🏻")]
    [InlineData("...")]
    public void AReactionEmojiIsNotAWaitingCustomer(string preview)
    {
        var verdict = ReplyNeed.Classify(preview);

        Assert.False(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.EmojiOnly, verdict.Reason);
    }

    [Theory]
    [InlineData("Ok thnx 👍🏻")]
    [InlineData("Ok fine no problem🖤😁")]
    [InlineData("Your welcome 😊")]
    [InlineData("Oky...")]
    [InlineData("Sure.....")]
    public void PunctuationAndEmojiDoNotStopAMessageBeingRecognised(string preview)
    {
        // Skin-tone modifiers and variation selectors used to survive tokenisation as invisible words,
        // which meant a closer with an emoji stuck on the end read as unrecognised text.
        Assert.False(ReplyNeed.Classify(preview).NeedsReply, $"'{preview}' was not recognised as a closer.");
    }

    // ---- Urdu script ----------------------------------------------------------------------------------

    [Fact]
    public void UrduScriptIsHandledAsWellAsRomanUrdu()
    {
        Assert.False(ReplyNeed.Classify("شکریہ").NeedsReply);
        Assert.False(ReplyNeed.Classify("ٹھیک ہے").NeedsReply);

        Assert.True(ReplyNeed.Classify("کتنا وقت").NeedsReply);
        Assert.True(ReplyNeed.Classify("قیمت کیا ہے").NeedsReply);
    }

    // ---- The property the design rests on -------------------------------------------------------------

    [Fact]
    public void AsksSomethingAlwaysWinsOverEveryClosingRule()
    {
        // Exhaustive rather than by example: every closing phrase in the tests above, with a question
        // appended, must come back as a question. If a new closing rule is ever added that can outrank
        // the request check, this is what catches it.
        string[] closers = ["ok", "thanks", "ji", "walaikum us salam", "👍", "sure", "okay tysm"];
        string[] questions = ["kitna", "what time", "?", "price", "kab", "send address"];

        foreach (var closer in closers)
        {
            foreach (var question in questions)
            {
                var combined = $"{closer} {question}";
                var verdict = ReplyNeed.Classify(combined);

                Assert.True(verdict.NeedsReply, $"'{combined}' was closed despite asking something.");
                Assert.Equal(ReplyNeedReason.AsksSomething, verdict.Reason);
            }
        }
    }

    [Fact]
    public void EveryReasonHasPlainEnglishForTheOwner()
    {
        // The excluded list has to say WHY, or it is just a number the owner cannot check.
        foreach (ReplyNeedReason reason in Enum.GetValues<ReplyNeedReason>())
        {
            var text = new ReplyNeedVerdict(false, reason).Explain();

            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("_", text, StringComparison.Ordinal);
        }
    }
}
