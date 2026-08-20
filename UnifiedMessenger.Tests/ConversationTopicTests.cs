using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Sorting the waiting queue by what the customer wants. Every example is verbatim from real traffic.
///
/// <para>
/// The categories were chosen from that traffic rather than from a generic sales taxonomy: "urgent / leads
/// / follow-up" would have missed the two largest removable groups — people asking about jobs or replying
/// to a training form, and inbound B2B outreach — which together are more of this owner's queue than
/// bookings and enquiries combined.
/// </para>
/// </summary>
public class ConversationTopicTests
{
    // ---- The three that matter most -------------------------------------------------------------------

    [Theory]
    [InlineData("V v v unprofessional staff Came for waxing yesterday My girls got bruises on legs")]
    [InlineData("Warna main kahin aur chali jawon")]
    [InlineData("Thanku bhot expensive hn ap")]
    public void TheMessagesThatCostCustomersAreFoundFirst(string preview)
    {
        // These three were sitting unanswered inside a list of 466 and are the entire reason this filter
        // exists. A complaint, a churn threat, and a price objection.
        Assert.Equal(ConversationTopic.AtRisk, ConversationTopics.Classify(preview));
    }

    [Fact]
    public void AComplaintThatAlsoMentionsPriceIsAComplaintNotAnEnquiry()
    {
        // Order of the checks is load-bearing: AtRisk wins outright.
        Assert.Equal(
            ConversationTopic.AtRisk,
            ConversationTopics.Classify("your rates are too much and the staff was rude"));
    }

    // ---- The false positives the real-data probe caught ------------------------------------------------

    [Theory]
    [InlineData("Madiha rashid")]
    [InlineData("Farhan Rashid")]
    [InlineData("Ali Burney")]
    [InlineData("Spain trip")]
    [InlineData("Poornima")]
    public void ACommonSurnameIsNotAComplaint(string preview)
    {
        // "rash" inside "Rashid", "burn" inside "Burney", "pain" inside "Spain", "poor" inside "Poornima".
        // Rashid and Burney are common surnames here, so plain substring matching flagged real customers
        // as complaints — in the one list that has to be trustworthy. Single words are matched whole.
        Assert.NotEqual(ConversationTopic.AtRisk, ConversationTopics.Classify(preview));
    }

    [Fact]
    public void AnotherBusinessesAutoReplyIsOutreachNotAJobApplication()
    {
        // Caught by the probe: "intern" inside "International" filed a clinic's out-of-hours responder
        // under job applicants.
        var preview = "Thank you for contacting Medspine International. Our team is currently unavailable. "
            + "Please leave your message, and we will respond shortly.";

        Assert.Equal(ConversationTopic.BusinessOutreach, ConversationTopics.Classify(preview));
    }

    [Fact]
    public void AWordStillMatchesThroughPunctuationAndInflection()
    {
        // The whole-word rule must not become brittle. Boundaries are non-alphanumeric, so trailing commas
        // and brackets are fine.
        Assert.Equal(ConversationTopic.AtRisk, ConversationTopics.Classify("bohat (expensive), sorry"));
        Assert.Equal(ConversationTopic.AtRisk, ConversationTopics.Classify("mehngi!"));
    }

    // ---- The groups worth hiding ----------------------------------------------------------------------

    [Theory]
    [InlineData("I need jop dear")]
    [InlineData("Job chahiye")]
    [InlineData("Im interested for job")]
    [InlineData("I fill the form")]
    [InlineData("I submitted")]
    [InlineData("Agr kindly plz ap ki kisi or branch may job available hai to batya dy")]
    [InlineData("Islamabad mein koi course hai open 8 pass")]
    [InlineData("Arzoo Adnan Cv.pdf")]
    public void PeopleAskingAboutWorkAreNotCustomerService(string preview)
    {
        Assert.Equal(ConversationTopic.JobApplicant, ConversationTopics.Classify(preview));
    }

    [Theory]
    [InlineData("Hello! I'm reaching out from SJP Photography_Films. I'd love to collab")]
    [InlineData("Assalam-o-Alaikum, I'm from AestheticFlow. I reviewed your profile")]
    [InlineData("Scan the QR code to continue the process of sharing your WhatsApp contact")]
    public void InboundBusinessOutreachIsItsOwnCategory(string preview)
    {
        Assert.Equal(ConversationTopic.BusinessOutreach, ConversationTopics.Classify(preview));
    }

    [Fact]
    public void APitchQuotingItsOwnRatesIsNotACustomerAskingAboutRates()
    {
        // Outreach is checked before the commercial topics for exactly this case.
        Assert.Equal(
            ConversationTopic.BusinessOutreach,
            ConversationTopics.Classify("We can promote your salon — our rates start at 20k/month"));
    }

    // ---- Money on the table ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Aaj ka nahae pocha rahie wasie kiya charge kerain gaye")]
    [InlineData("Any chance of discount")]
    [InlineData("whats your services")]
    [InlineData("kitna charge hoga")]
    public void PriceAndServiceQuestionsAreEnquiries(string preview)
    {
        Assert.Equal(ConversationTopic.Enquiry, ConversationTopics.Classify(preview));
    }

    [Theory]
    [InlineData("Ill be there around 12.30 pm")]
    [InlineData("can I book for tomorrow")]
    [InlineData("I want to reschedule my appointment")]
    public void ArrangingAVisitIsABooking(string preview)
    {
        Assert.Equal(ConversationTopic.Booking, ConversationTopics.Classify(preview));
    }

    // ---- Honesty about what it does not know ---------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("ok")]
    [InlineData("Mel to mel")]
    [InlineData("Near chandni chok")]
    public void WhatItCannotIdentifyIsReportedAsUnknownRatherThanGuessed(string? preview)
    {
        // 432 of the owner's 468 land here, and saying so is the point. A filter that guessed a topic for
        // every row would look more capable and be worth less — the owner has to be able to trust the
        // three rows in "At risk".
        Assert.Equal(ConversationTopic.Unknown, ConversationTopics.Classify(preview));
    }

    [Fact]
    public void EveryTopicHasALabelAndAnExplanation()
    {
        foreach (ConversationTopic topic in Enum.GetValues<ConversationTopic>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ConversationTopics.Label(topic)));
            Assert.False(string.IsNullOrWhiteSpace(ConversationTopics.Describe(topic)));
        }
    }

    [Fact]
    public void TopicNeverChangesWhetherAReplyIsOwed()
    {
        // The two classifiers are deliberately separate. A filter that could alter a count would let the
        // owner change the numbers by clicking a chip.
        foreach (var preview in new[] { "ok thanks", "kitna charge hoga", "I need jop dear", "" })
        {
            var before = ReplyNeed.Classify(preview);
            ConversationTopics.Classify(preview);
            var after = ReplyNeed.Classify(preview);

            Assert.Equal(before, after);
        }
    }
}

/// <summary>
/// The five rules built after reading what was actually inside the "Uncategorised" bucket — 79 of the 205
/// waiting conversations on the owner's real queue, run through the production classifiers.
/// </summary>
/// <remarks>
/// It was not junk. It was misfiled work: about a quarter were a bare customer name (the booking flow),
/// roughly ten were Roman Urdu enquiries the lexicon did not cover, several were business outreach, a
/// handful were acknowledgements with typos that should never have been queued, and a few were attachment
/// filenames. Worst of all, "At risk" showed <b>1</b> while three customers chasing an unanswered message
/// sat unclassified.
/// </remarks>
public class UncategorisedRecoveryTests
{
    // ---- 1 · being ignored is a churn signal -------------------------------------------------------

    [Theory]
    [InlineData("Apny koi reply nhi kiya dubara")]   // you didn't reply again
    [InlineData("Reply me please")]
    [InlineData("Or else share your pr team number")]
    [InlineData("still waiting for your response")]
    [InlineData("no reply from your side")]
    [InlineData("Koi jawab nahi mila")]
    public void ChasingAnUnansweredMessageIsAtRisk(string preview) =>
        Assert.Equal(ConversationTopic.AtRisk, ConversationTopics.Classify(preview));

    // ---- 2 · a bare name is the booking flow -------------------------------------------------------

    [Theory]
    [InlineData("Aiza Anwar")]
    [InlineData("Hira Sabir")]
    [InlineData("Palwasha zaib")]
    [InlineData("Talha Uzair")]
    [InlineData("Saima Rustum Ali")]
    [InlineData("Madiha rashid")]
    public void ABareNameIsTreatedAsABooking(string preview) =>
        Assert.Equal(ConversationTopic.Booking, ConversationTopics.Classify(preview));

    [Theory]
    [InlineData("For mens")]
    [InlineData("See you")]
    [InlineData("No need")]
    [InlineData("Send me here")]
    [InlineData("What is this")]
    [InlineData("Yes mam")]
    [InlineData("On 7")]                 // has a digit
    [InlineData("Possible hai ?")]       // has a question mark
    [InlineData("Madiha")]               // single word — too ambiguous to guess
    public void OrdinaryShortPhrasesAreNotMistakenForNames(string preview) =>
        Assert.NotEqual(ConversationTopic.Booking, ConversationTopics.Classify(preview));

    [Fact]
    public void ARealTopicAlwaysOutranksTheNameRule()
    {
        // The name rule runs last precisely so it cannot outrank anything that actually says something.
        Assert.Equal(ConversationTopic.AtRisk, ConversationTopics.Classify("Sadia Naeem complaint"));
        Assert.Equal(ConversationTopic.Enquiry, ConversationTopics.Classify("Hira Sabir price"));
        Assert.Equal(ConversationTopic.JobApplicant, ConversationTopics.Classify("Talha Uzair cv"));
    }

    [Fact]
    public void TheNameRuleOnlyMovesARowItNeverClosesOne()
    {
        // Booking still needs a reply. Nothing in this rule can drop a customer.
        Assert.True(ReplyNeed.Classify("Aiza Anwar").NeedsReply);
    }

    // ---- 3 · Roman Urdu enquiries ------------------------------------------------------------------

    [Theory]
    [InlineData("AP srves dety ho")]          // do you provide services
    [InlineData("Bramch kb close hgi")]       // when does the branch close
    [InlineData("Aap please mujha actual amount mention kr dain")]
    [InlineData("Is it applicable to scheme 3 branch")]
    [InlineData("Both signature and senior artist")]
    [InlineData("Could you tell me the name of the stylist that did my hair just now?")]
    [InlineData("Loyalty card")]
    public void RomanUrduAndSalonVocabularyReadAsEnquiries(string preview) =>
        Assert.Equal(ConversationTopic.Enquiry, ConversationTopics.Classify(preview));

    // ---- 4 · acknowledgement typos should never have been queued -----------------------------------

    [Theory]
    [InlineData("Ohky")]
    [InlineData("Okhy")]
    [InlineData("Ok thankd")]
    [InlineData("Appreciated")]
    [InlineData("Yuppp. Acknowledged")]
    public void MisspelledSignOffsCloseTheConversation(string preview)
    {
        var verdict = ReplyNeed.Classify(preview);

        Assert.False(verdict.NeedsReply);
    }

    [Fact]
    public void AQuestionAttachedToAnAcknowledgementStillCounts()
    {
        // The rule that must not regress: AsksSomething overrides every closer.
        Assert.True(ReplyNeed.Classify("Ohky but what time?").NeedsReply);
    }

    // ---- 5 · attachment filenames ------------------------------------------------------------------

    [Theory]
    [InlineData("Islamabad.pdf")]
    [InlineData("PERIO IMP STATIONS .pdf")]
    [InlineData("VID_20260816_145608.mp4")]
    [InlineData("8.jfif")]
    public void AnAttachmentNameIsFiledAsMedia(string preview) =>
        Assert.Equal(QueueFacet.Media, QueueFacets.Resolve(ReplyNeedReason.Substantive, preview));

    [Fact]
    public void ASentenceThatMerelyMentionsAFileIsStillClassifiedByWhatItSays()
    {
        Assert.Equal(
            QueueFacet.Enquiry,
            QueueFacets.Resolve(ReplyNeedReason.Substantive, "can you send me the price list please invoice.pdf"));
    }
}
