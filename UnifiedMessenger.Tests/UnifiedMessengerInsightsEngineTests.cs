using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection("UnifiedMessengerSerial")]
public class UnifiedMessengerInsightsEngineTests : IDisposable
{
    private readonly bool _originalEnableLocalAi;

    public UnifiedMessengerInsightsEngineTests()
    {
        _originalEnableLocalAi = AppSettingsService.Instance.Settings.EnableLocalAi;
        AppSettingsService.Instance.Settings.EnableLocalAi = true;
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.EnableLocalAi = _originalEnableLocalAi;
    }
    [Fact]
    public async Task ProcessJob_HeuristicFallbackEnrichesOperationalFields()
    {
        var triage = new MessageTriageService();
        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "branch-insights",
                Platform = "whatsapp",
                MessageText = "What is your bridal makeup rate?",
                CustomerName = "Nida"
            },
            "DHA-2");

        var item = triage.GetAllItems()[0];
        var engine = new UnifiedMessengerInsightsEngine(
            new MessageTriageInferenceRunner(new StubTriageLlmClient(null)),
            triage);

        await engine.ProcessJobForTestsAsync(new UnifiedMessengerInsightsJob
        {
            Kind = UnifiedMessengerInsightsJobKind.MessageAnalysis,
            InstanceId = item.InstanceId,
            TriageItemId = item.Id
        });

        var updated = triage.GetAllItems()[0];
        Assert.Equal(UnifiedMessengerIntentCategory.PriceInquiry, updated.AiIntentCategory);
        Assert.True(updated.EstimatedValue > 0);
        Assert.False(string.IsNullOrWhiteSpace(updated.NextActionSummary));
    }

    [Fact]
    public async Task ProcessJob_UpgradesWithLlmAndTranscriptEnrichment()
    {
        const string json = """
            {"UrgencyScore":72,"Sentiment":"Neutral","CustomerIntent":"Inquiry","ExtractedEntities":{},"CoreSummary":"Price inquiry for bridal","AiIntentCategory":"Price_Inquiry","ClientSentiment":"Neutral","OperationalUrgency":3,"EstimatedValue":0,"NextActionSummary":"","IsRevenueLeakageRisk":false}
            """;

        var triage = new MessageTriageService();
        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "branch-insights-2",
                Platform = "whatsapp",
                MessageText = "Share bridal package pricing",
                CustomerName = "Hina"
            },
            "F-11");

        var item = triage.GetAllItems()[0];
        var engine = new UnifiedMessengerInsightsEngine(
            new MessageTriageInferenceRunner(new StubTriageLlmClient(json)),
            triage);

        await engine.ProcessJobForTestsAsync(new UnifiedMessengerInsightsJob
        {
            Kind = UnifiedMessengerInsightsJobKind.MessageAnalysis,
            InstanceId = item.InstanceId,
            TriageItemId = item.Id
        });

        var updated = triage.GetAllItems()[0];
        Assert.Equal(TriageInferenceSource.LocalAi, updated.InferenceSource);
        Assert.Equal(UnifiedMessengerIntentCategory.PriceInquiry, updated.AiIntentCategory);
        Assert.True(updated.EstimatedValue >= 15000);
    }

    private sealed class StubTriageLlmClient(string? payload) : ITriageLlmClient
    {
        public Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken) =>
            Task.FromResult(payload);
    }
}
