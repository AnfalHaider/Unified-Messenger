using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class UnifiedMessengerInsightsAnalyzerTests
{
    [Fact]
    public void Enrich_DetectsHangingLeadFromOutboundQuoteTranscript()
    {
        const string transcript = """
            [incoming] Do you have bridal packages?
            [outgoing] Our bridal package is PKR 85,000 with slot available tomorrow.
            """;

        var job = CreateJob("Thanks, I'll think about it.");
        var response = new RichTriageLlmResponse
        {
            UrgencyScore = 20,
            Sentiment = "Neutral",
            CustomerIntent = "Inquiry",
            AiIntentCategory = UnifiedMessengerIntentCategory.Inquiry,
            ClientSentiment = ClientSentimentLabel.Neutral,
            OperationalUrgency = 2
        };

        var enriched = UnifiedMessengerInsightsAnalyzer.Enrich(response, job, transcript);

        Assert.True(enriched.IsRevenueLeakageRisk);
        Assert.True(enriched.EstimatedValue >= 85000);
        Assert.False(string.IsNullOrWhiteSpace(enriched.NextActionSummary));
    }

    [Fact]
    public void ApplyOperationalInsights_BoostsComplaintUrgencyWhenLlmUnavailable()
    {
        var item = new MessageTriageItem
        {
            Id = "a|1",
            InstanceId = "a",
            InstanceDisplayName = "Branch A",
            Platform = "whatsapp",
            MessagePreview = "This is unacceptable — I want a refund now.",
            CustomerName = "Sara",
            UrgencyScore = 40,
            Sentiment = MessageSentiment.Negative,
            OperationalUrgency = 2,
            AiIntentCategory = UnifiedMessengerIntentCategory.Inquiry,
            ClientSentiment = ClientSentimentLabel.Neutral
        };

        var enriched = UnifiedMessengerInsightsAnalyzer.ApplyOperationalInsights(item);

        Assert.Equal(UnifiedMessengerIntentCategory.Complaint, enriched.AiIntentCategory);
        Assert.Equal(ClientSentimentLabel.Critical, enriched.ClientSentiment);
        Assert.Equal(5, enriched.OperationalUrgency);
        Assert.Contains("Sara", enriched.NextActionSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRevenueLeakageRisk_RequiresThirtyMinuteLatencyForCommercialIntent()
    {
        Assert.True(UnifiedMessengerInsightsAnalyzer.ResolveRevenueLeakageRisk(
            UnifiedMessengerIntentCategory.Booking,
            latencyMinutes: 45,
            corpus: "Need a slot for Saturday"));

        Assert.False(UnifiedMessengerInsightsAnalyzer.ResolveRevenueLeakageRisk(
            UnifiedMessengerIntentCategory.Booking,
            latencyMinutes: 10,
            corpus: "Need a slot for Saturday"));
    }

    [Fact]
    public void ConversationNoiseFilter_StripsEncryptedBanner()
    {
        var cleaned = ConversationNoiseFilter.Strip(
            "Messages and calls are end-to-end encrypted. Only people in this chat can read them.");

        Assert.Equal(string.Empty, cleaned);
    }

    private static RichTriageInferenceJob CreateJob(string message) =>
        new()
        {
            TriageItemId = "item-1",
            InstanceId = "branch-a",
            InstanceDisplayName = "Branch A",
            Platform = "whatsapp",
            MessageText = message,
            CustomerName = "Ayesha"
        };
}
