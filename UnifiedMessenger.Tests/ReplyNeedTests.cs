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

    [Fact]
    public void ASharedContactCardIsCountedAndNotMistakenForAQuestion()
    {
        // 22 of the owner's waiting chats showed a raw "102074813546715@lid" where the message text
        // belongs. ChatEntryParser now labels it; this pins that the label does not then get read as the
        // customer asking to be contacted, which the word "contact" would otherwise have done.
        var verdict = ReplyNeed.Classify("Shared a contact");

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MediaWithoutCaption, verdict.Reason);
    }

    // ---- Telling "the message is gone" from "we have not read it yet" --------------------------------

    [Fact]
    public void AConversationWhoseMessageNoLongerExistsIsNotAWaitingCustomer()
    {
        // Observed live by the owner: a chat 57 days old, no preview, and nothing at all in the thread when
        // opened — the message had been deleted or had expired under disappearing messages. There is
        // nothing to reply to, so counting it as a customer waiting is simply wrong.
        var verdict = ReplyNeed.Classify(
            "", hasLastMessage: false, lastMessageType: "", waitingFor: TimeSpan.FromDays(57));

        Assert.False(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MessageNoLongerAvailable, verdict.Reason);
    }

    [Fact]
    public void AnUncaptionedPhotoIsNeverMistakenForAMissingMessage()
    {
        // This is the trap the first version of the fix walked into. The scraper's bodyOf() returns an
        // empty string for an uncaptioned photo AND for a message that does not exist, so closing on a
        // blank preview plus age would have silently dropped every customer who sent a picture — and a
        // picture is very often "can you do this?".
        var verdict = ReplyNeed.Classify(
            "", hasLastMessage: true, lastMessageType: "image", waitingFor: TimeSpan.FromDays(57));

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MediaWithoutCaption, verdict.Reason);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    [InlineData("ptt")]
    [InlineData("audio")]
    [InlineData("document")]
    [InlineData("sticker")]
    public void EveryWordlessMessageKindStillCounts(string type)
    {
        Assert.True(ReplyNeed.Classify("", true, type, TimeSpan.FromDays(30)).NeedsReply);
    }

    [Fact]
    public void ARecentBlankConversationKeepsItsPlace()
    {
        // A chat from an hour ago with no body may genuinely still be syncing — the store fills message
        // bodies in lazily. Only age makes "no message" credible, so recent ones are never closed.
        var verdict = ReplyNeed.Classify(
            "", hasLastMessage: false, lastMessageType: "", waitingFor: TimeSpan.FromHours(1));

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.NoPreviewAvailable, verdict.Reason);
    }

    [Fact]
    public void AnOlderBuildsSnapshotIsNeverMassClosed()
    {
        // hasLastMessage is null on a snapshot written before the field existed. Treating unknown as
        // "no message" would clear an upgrading install's entire queue on first load, which is the worst
        // possible first impression of an accuracy fix.
        var verdict = ReplyNeed.Classify(
            "", hasLastMessage: null, lastMessageType: "", waitingFor: TimeSpan.FromDays(90));

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.NoPreviewAvailable, verdict.Reason);
    }

    [Fact]
    public void RealTextAlwaysWinsOverTheMessageExistenceSignals()
    {
        // If there IS text, none of this applies — a scraper that reports hasLastMessage inconsistently
        // must not be able to discard a message the app can plainly read.
        var verdict = ReplyNeed.Classify(
            "kitna charge hoga", hasLastMessage: false, lastMessageType: "", waitingFor: TimeSpan.FromDays(90));

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.AsksSomething, verdict.Reason);
    }

    // ---- Entries that are not messages ---------------------------------------------------------------

    [Theory]
    [InlineData("e2e_notification")]
    [InlineData("protocol")]
    [InlineData("notification_template")]
    [InlineData("gp2")]
    [InlineData("ciphertext")]
    [InlineData("keychange")]
    public void WhatsAppsOwnBookkeepingIsNotACustomerWaiting(string type)
    {
        // 39 of 212 conversations the app reported as customers waiting were security-code changes and
        // protocol notices. Nobody wrote them, so nobody is waiting on an answer — and because they carry
        // no body they were also inflating the "could not read this message" count.
        var verdict = ReplyNeed.Classify("", true, type, TimeSpan.FromDays(3));

        Assert.False(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.SystemNotice, verdict.Reason);
    }

    [Fact]
    public void ASystemNoticeIsNotCountedEvenIfItCarriesText()
    {
        // WhatsApp puts its own wording in some of these ("Your security code changed"), which would
        // otherwise be read as a substantive customer message.
        var verdict = ReplyNeed.Classify(
            "Your security code with this contact changed", true, "e2e_notification", TimeSpan.FromDays(1));

        Assert.False(verdict.NeedsReply);
    }

    [Theory]
    [InlineData("call_log")]
    [InlineData("call")]
    public void AMissedCallIsStillWorthReturningButIsNamedAsACall(string type)
    {
        // 36 of the 212 were calls appearing as messages with no readable text, so the row told the owner
        // nothing about what had actually happened. A missed customer call is worth returning, so it stays
        // counted — it just stops pretending to be a message nobody could read.
        var verdict = ReplyNeed.Classify("", true, type, TimeSpan.FromDays(3));

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MissedCall, verdict.Reason);
        Assert.Contains("called", verdict.Explain(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnOrdinaryTextMessageIsUnaffectedByTheTypeChecks()
    {
        Assert.Equal(
            ReplyNeedReason.AsksSomething,
            ReplyNeed.Classify("kitna charge hoga", true, "chat", TimeSpan.FromHours(2)).Reason);
        Assert.Equal(
            ReplyNeedReason.Acknowledgement,
            ReplyNeed.Classify("ok thanks", true, "chat", TimeSpan.FromHours(2)).Reason);
    }
}

/// <summary>
/// Call-log entries, and the direction bug found by reading the owner's real WhatsApp store over CDP.
/// </summary>
/// <remarks>
/// The branch matched any <c>call_log</c> and returned "missed call · needs reply · Call back" — including
/// calls the salon itself placed. Measured live on the owner's accounts: one session had 3 of 19 call-log
/// entries outbound, another had 7 of 7. Those were all being queued as customers to ring back.
///
/// Verified at the same time, and worth recording because it saved shipping a wrong fix: WhatsApp's message
/// model does <b>not</b> carry the call outcome in <c>subtype</c> — it reads <c>undefined</c> on every real
/// call entry. The outcome lives behind <c>WAWebCallLogUtils.getIsMissedCallOrNotConnected</c>, against a
/// call-log record the message does not directly expose. So "accepted on another device" is still counted;
/// direction is what could be fixed honestly today.
/// </remarks>
public class CallLogDirectionTests
{
    [Theory]
    [InlineData("call_log")]
    [InlineData("call")]
    public void AnIncomingCallStillNeedsCallingBack(string type)
    {
        var verdict = ReplyNeed.Classify("Voice call", true, type, TimeSpan.FromHours(2), lastMessageFromMe: false);

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MissedCall, verdict.Reason);
    }

    [Theory]
    [InlineData("call_log")]
    [InlineData("call")]
    public void ACallWePlacedIsNotSomeoneToRingBack(string type)
    {
        var verdict = ReplyNeed.Classify("Voice call", true, type, TimeSpan.FromHours(2), lastMessageFromMe: true);

        Assert.False(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.OutgoingCall, verdict.Reason);
    }

    [Fact]
    public void UnknownDirectionStaysCounted()
    {
        // The bias of this whole classifier is one-directional: close only on positive evidence. An older
        // snapshot with no direction recorded must not silently drop a real missed call.
        var verdict = ReplyNeed.Classify("Voice call", true, "call_log", TimeSpan.FromHours(2), lastMessageFromMe: null);

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MissedCall, verdict.Reason);
    }

    [Fact]
    public void TheOutgoingExplanationSaysWhyNothingIsWaiting()
    {
        var verdict = new ReplyNeedVerdict(false, ReplyNeedReason.OutgoingCall);

        Assert.Contains("You called them", verdict.Explain(), StringComparison.Ordinal);
    }
}

/// <summary>
/// The call OUTCOME — the half that could not be fixed without reading WhatsApp's live model.
/// </summary>
/// <remarks>
/// Owner-reported: "Voice call — Accepted on another device" appeared under Missed calls with a Call back
/// button, for a customer they had already spoken to on their phone.
///
/// The vocabulary here was read over CDP from 378 real call entries on the owner's own accounts, never
/// guessed — and the guess would have been wrong: WhatsApp does not put this in <c>subtype</c>, which is
/// <c>undefined</c> on every call entry. It is <c>message.callOutcome</c>.
///
/// The measured distribution of the 317 INBOUND calls in that sample:
/// <code>
///   Missed             166
///   Completed          102
///   AcceptedElsewhere   33
///   Rejected            14
///   Ongoing / Failed     2
/// </code>
/// Only 52% were actually missed. The app was asking the owner to ring back all of them.
/// </remarks>
public class CallOutcomeTests
{
    private static ReplyNeedVerdict Call(string outcome, bool fromMe = false) =>
        ReplyNeed.Classify("Voice call", true, "call_log", TimeSpan.FromHours(3), fromMe, outcome);

    [Theory]
    [InlineData("Completed")]
    [InlineData("AcceptedElsewhere")]
    [InlineData("acceptedelsewhere")]
    [InlineData("Ongoing")]
    public void AnAnsweredCallIsNotSomeoneToRingBack(string outcome)
    {
        var verdict = Call(outcome);

        Assert.False(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.CallAnswered, verdict.Reason);
    }

    [Theory]
    [InlineData("Missed")]
    [InlineData("Failed")]
    public void ACallThatNeverConnectedStillNeedsReturning(string outcome)
    {
        var verdict = Call(outcome);

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MissedCall, verdict.Reason);
    }

    [Fact]
    public void ADeclinedCallStaysCounted()
    {
        // Rejected means someone actively declined it. The customer still did not get what they rang for,
        // so this is NOT treated as answered — closing it would be the one mistake this classifier is
        // built to avoid.
        var verdict = Call("Rejected");

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MissedCall, verdict.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("SomethingWhatsAppAddedLater")]
    public void AnUnknownOutcomeStaysCounted(string? outcome)
    {
        // The IndexedDB fallback cannot read the outcome at all, and WhatsApp may add values. Unknown must
        // never silently close a real missed call.
        var verdict = Call(outcome!);

        Assert.True(verdict.NeedsReply);
        Assert.Equal(ReplyNeedReason.MissedCall, verdict.Reason);
    }

    [Fact]
    public void DirectionStillWinsOverOutcome()
    {
        // A call we placed is ours regardless of how it ended.
        Assert.Equal(ReplyNeedReason.OutgoingCall, Call("Missed", fromMe: true).Reason);
        Assert.Equal(ReplyNeedReason.OutgoingCall, Call("Completed", fromMe: true).Reason);
    }

    [Fact]
    public void TheAnsweredExplanationSaysSomeonePickedUp()
    {
        Assert.Contains(
            "was answered",
            new ReplyNeedVerdict(false, ReplyNeedReason.CallAnswered).Explain(),
            StringComparison.Ordinal);
    }
}
