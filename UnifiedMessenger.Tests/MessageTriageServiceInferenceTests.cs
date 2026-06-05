using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class MessageTriageServiceInferenceTests
{
    [Fact]
    public async Task ProcessInference_KeepsHeuristicWhenLlmPayloadFails()
    {
        var service = new MessageTriageService(new MessageTriageInferenceRunner(new StubTriageLlmClient(null)));
        var instances = CreateInstances("branch-a");

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "branch-a",
                Platform = "metabusiness",
                MessageText = "Please confirm my appointment tomorrow."
            },
            "Branch A");

        var baseline = service.BuildSnapshot(instances).UrgentQueue[0];
        var job = new RichTriageInferenceJob
        {
            TriageItemId = baseline.Id,
            InstanceId = baseline.InstanceId,
            InstanceDisplayName = baseline.InstanceDisplayName,
            Platform = baseline.Platform,
            MessageText = "Please confirm my appointment tomorrow.",
            HeuristicUrgencyScore = baseline.UrgencyScore,
            HeuristicSentiment = baseline.Sentiment
        };

        await service.ProcessInferenceForTestsAsync(job);

        var after = service.BuildSnapshot(instances).UrgentQueue[0];
        Assert.Equal(TriageInferenceSource.Heuristic, after.InferenceSource);
        Assert.Equal(baseline.UrgencyScore, after.UrgencyScore);
        Assert.Equal(UnifiedMessengerIntentCategory.Booking, after.AiIntentCategory);
        Assert.False(string.IsNullOrWhiteSpace(after.NextActionSummary));
    }

    [Fact]
    public async Task ProcessInference_UpgradesItemWhenLlmReturnsValidJson()
    {
        const string json = """
            {"UrgencyScore":95,"Sentiment":"Negative","CustomerIntent":"Complaint","ExtractedEntities":{"CustomerName":"Sara","ContactNumber":null,"RequestedDate":null,"RequestedTime":null,"ServiceType":null,"ActionRequired":"call"},"CoreSummary":"Urgent cancellation request","AiIntentCategory":"Complaint","ClientSentiment":"Critical","OperationalUrgency":5,"EstimatedValue":25000,"NextActionSummary":"Call Sara immediately","IsRevenueLeakageRisk":false}
            """;
        var service = new MessageTriageService(new MessageTriageInferenceRunner(new StubTriageLlmClient(json)));
        var instances = CreateInstances("branch-a");

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "branch-a",
                Platform = "metabusiness",
                MessageText = "Cancel my booking immediately, this is urgent.",
                CustomerName = "Sara"
            },
            "Branch A");

        var baseline = service.BuildSnapshot(instances).UrgentQueue[0];
        Assert.Equal(TriageInferenceSource.Heuristic, baseline.InferenceSource);

        var job = new RichTriageInferenceJob
        {
            TriageItemId = baseline.Id,
            InstanceId = baseline.InstanceId,
            InstanceDisplayName = baseline.InstanceDisplayName,
            Platform = baseline.Platform,
            MessageText = "Cancel my booking immediately, this is urgent.",
            CustomerName = "Sara",
            HeuristicUrgencyScore = baseline.UrgencyScore,
            HeuristicSentiment = baseline.Sentiment
        };

        await service.ProcessInferenceForTestsAsync(job);

        var after = service.BuildSnapshot(instances).UrgentQueue[0];
        Assert.Equal(TriageInferenceSource.LocalAi, after.InferenceSource);
        Assert.Equal(95, after.UrgencyScore);
        Assert.Equal("Critical", after.UrgencyLabel);
        Assert.Equal(CustomerIntent.Complaint, after.CustomerIntent);
        Assert.Equal("Urgent cancellation request", after.CoreSummary);
        Assert.Equal(ClientSentimentLabel.Critical, after.ClientSentiment);
        Assert.Equal(5, after.OperationalUrgency);
        Assert.Equal(25000, after.EstimatedValue);
    }

    private static MessengerInstance[] CreateInstances(string id) =>
    [
        new MessengerInstance
        {
            Id = id,
            DisplayName = "Branch A",
            ProfileName = id,
            Platform = "metabusiness",
            StartUrl = "https://example.com",
            Category = WorkspaceCategory.Professional
        }
    ];

    private sealed class StubTriageLlmClient(string? payload) : ITriageLlmClient
    {
        public Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken) =>
            Task.FromResult(payload);
    }
}
