using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ConversationNoiseFilterTests
{
    [Theory]
    [InlineData("1:47 AM Assalamualikum we need bridal makeup", "we need bridal makeup")]
    [InlineData("Sara: 10:15 PM Need pricing for Saturday", "Need pricing for Saturday")]
    [InlineData("Assalamualikum", "")]
    [InlineData("Hi", "")]
    public void CleanForInference_StripsTimestampsAndGreetings(string input, string expected)
    {
        var cleaned = ConversationNoiseFilter.CleanForInference(input);
        Assert.Equal(expected, cleaned);
        Assert.False(ConversationNoiseFilter.ContainsTimestampNoise(cleaned));
    }

    [Fact]
    public void IsPromoSpam_DetectsB2BMarketingMessage()
    {
        const string spam =
            "We create custom foldable promo cards for brands. Perfect for packaging, delivery bags. Mini campaign!";

        Assert.True(ConversationNoiseFilter.IsPromoSpam(spam));
    }

    [Fact]
    public void IsPromoSpam_DoesNotFlagBridalBookingInquiry()
    {
        const string booking =
            "Hi, I need an urgent bridal makeup slot for this Saturday at the F-11 branch. What are your charges?";

        Assert.False(ConversationNoiseFilter.IsPromoSpam(booking));
    }
}

public class AiPipelineRoutingTests
{
    [Fact]
    public void SpamMessage_IsExcludedFromImmediateQueueAndSla()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var spamItem = new UnifiedMessenger.Models.MessageTriageItem
        {
            Id = "inst|spam",
            InstanceId = "inst",
            InstanceDisplayName = "Depilex F-11",
            Platform = "whatsappbusiness",
            MessagePreview = "We create custom foldable promo cards for brands.",
            CustomerName = "Vendor",
            UrgencyScore = 5,
            Sentiment = UnifiedMessenger.Models.MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            IsSpamOrPromo = true,
            OperationalUrgency = 1,
            AiIntentCategory = UnifiedMessenger.Models.UnifiedMessengerIntentCategory.Spam,
            NextActionSummary = "Promotional message — no action required",
            SuggestedAction = "Ignore"
        };

        registry.UpsertFromTriageItem(spamItem, "Vendor", "F-11");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsSpamOrPromo);
        Assert.False(thread.IsImmediateAction);
        Assert.Equal(0, thread.LatencyMinutes);
        Assert.Equal(UnifiedMessenger.Models.UnifiedMessengerKanbanColumn.Resolved, thread.KanbanColumn);
    }

    [Fact]
    public void HighValueBooking_EntersImmediateQueueWithSummary()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var bookingItem = new UnifiedMessenger.Models.MessageTriageItem
        {
            Id = "inst|booking",
            InstanceId = "inst",
            InstanceDisplayName = "Depilex F-11",
            Platform = "whatsappbusiness",
            MessagePreview = "Urgent bridal makeup slot for Saturday at F-11.",
            CustomerName = "Aisha",
            UrgencyScore = 85,
            Sentiment = UnifiedMessenger.Models.MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            OperationalUrgency = 5,
            AiIntentCategory = UnifiedMessenger.Models.UnifiedMessengerIntentCategory.Booking,
            NextActionSummary = "Requests Saturday bridal makeup pricing at F-11.",
            SuggestedAction = "Reply with Pricing"
        };

        registry.UpsertFromTriageItem(bookingItem, "Aisha", "F-11");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsImmediateAction);
        Assert.Equal(5, thread.UrgencyScore);
        Assert.Contains("bridal makeup", thread.NextActionSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkThreadResolved_LocksReplyLatencyFromFirstInbound()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var inboundAt = DateTimeOffset.UtcNow.AddMinutes(-12);
        var item = new UnifiedMessenger.Models.MessageTriageItem
        {
            Id = "inst|reply",
            InstanceId = "inst",
            InstanceDisplayName = "Depilex DHA-2",
            Platform = "whatsappbusiness",
            MessagePreview = "Need quote for bridal package",
            CustomerName = "Sara",
            UrgencyScore = 45,
            Sentiment = UnifiedMessenger.Models.MessageSentiment.Neutral,
            TimestampUtc = inboundAt
        };

        registry.UpsertFromTriageItem(item, "Sara", "DHA-2");
        registry.MarkThreadResolved("inst", "Sara", "Sara", DateTimeOffset.UtcNow);

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsReplied);
        Assert.InRange(thread.LatencyMinutes, 11.5, 12.5);
        Assert.Equal(thread.LatencyMinutes, thread.ReplyLatencyMinutes);
    }

    [Fact]
    public void TryParseResponse_AcceptsCamelCaseSchema()
    {
        const string json = """
            {
              "isSpamOrPromo": false,
              "intentCategory": "Booking",
              "urgencyScore": 5,
              "actionableSummary": "Requests Saturday bridal makeup pricing at F-11.",
              "suggestedAction": "Reply with Pricing"
            }
            """;

        Assert.True(MessageTriageInferenceRunner.TryParseResponse(json, out var parsed));
        Assert.NotNull(parsed);
        Assert.False(parsed!.IsSpamOrPromo);
        Assert.Equal(5, parsed.OperationalUrgency);
        Assert.Contains("bridal makeup", parsed.ActionableSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseResponse_FlagsSpamSchema()
    {
        const string json = """
            {
              "isSpamOrPromo": true,
              "intentCategory": "Spam",
              "urgencyScore": 1,
              "actionableSummary": "Unsolicited promo — ignore.",
              "suggestedAction": "Ignore"
            }
            """;

        Assert.True(MessageTriageInferenceRunner.TryParseResponse(json, out var parsed));
        Assert.NotNull(parsed);
        Assert.True(parsed!.IsSpamOrPromo);
        Assert.Equal("Ignore", parsed.SuggestedAction);
    }
}
