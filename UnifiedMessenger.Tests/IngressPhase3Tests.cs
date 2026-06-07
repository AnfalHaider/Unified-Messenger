using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class IngressPhase3Tests
{
    [Theory]
    [InlineData("more_vert Flag as inappropriate", true)]
    [InlineData("Great service at the salon", false)]
    [InlineData("write a reply publicly", true)]
    public void IsDomChromePollution_DetectsUiChrome(string text, bool expected)
    {
        Assert.Equal(expected, ConversationNoiseFilter.IsDomChromePollution(text));
    }

    [Fact]
    public void UpsertFromTriageItem_StripsChromeFromNextActionSummary()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var item = new MessageTriageItem
        {
            Id = "inst|chrome",
            InstanceId = "inst",
            InstanceDisplayName = "Branch",
            Platform = "googlebusiness",
            MessagePreview = "Need appointment",
            CustomerName = "Sara",
            UrgencyScore = 40,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            NextActionSummary = "more_vert Flag as inappropriate"
        };

        registry.UpsertFromTriageItem(item, "review:abc123", "Branch");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(string.IsNullOrWhiteSpace(thread.NextActionSummary));
    }

    [Fact]
    public async Task ProcessRequest_DoesNotEnqueueInsightsForSpam()
    {
        var triage = new MessageTriageService();
        var insights = new UnifiedMessengerInsightsEngine(
            new MessageTriageInferenceRunner(new NoOpTriageLlmClient()),
            triage);

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "inst",
                Platform = "whatsappbusiness",
                MessageText =
                    "We create custom foldable promo cards for brands. Perfect for packaging, delivery bags. Mini campaign!"
            },
            "Branch");

        var job = new UnifiedMessengerInsightsJob
        {
            Kind = UnifiedMessengerInsightsJobKind.MessageAnalysis,
            InstanceId = "inst",
            TriageItemId = triage.GetAllItems()[0].Id
        };

        await insights.ProcessJobForTestsAsync(job);

        var item = triage.GetAllItems()[0];
        Assert.True(item.IsSpamOrPromo);
        Assert.Equal(TriageInferenceSource.Heuristic, item.InferenceSource);
    }

    private sealed class NoOpTriageLlmClient : ITriageLlmClient
    {
        public Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken,
            bool strictJsonRetry = false) =>
            Task.FromResult<string?>(
                """{"UrgencyScore":99,"Sentiment":"Negative","CustomerIntent":"Complaint","ExtractedEntities":{},"CoreSummary":"Should not run","AiIntentCategory":"Complaint","ClientSentiment":"Critical","OperationalUrgency":5,"EstimatedValue":0,"NextActionSummary":"Should not run","IsRevenueLeakageRisk":false}""");
    }
}
