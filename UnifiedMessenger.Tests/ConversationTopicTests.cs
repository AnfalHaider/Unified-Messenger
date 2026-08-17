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
