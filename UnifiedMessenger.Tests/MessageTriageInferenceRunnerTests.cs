using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests;

public class MessageTriageInferenceRunnerTests
{
    [Fact]
    public async Task TryInferAsync_CompletesConcurrentJobsThroughClient()
    {
        var client = new CountingTriageLlmClient(TimeSpan.FromMilliseconds(120));
        var runner = new MessageTriageInferenceRunner(client);
        var job = CreateJob("job-1");

        var jobTwo = CreateJob("job-2");
        var results = await Task.WhenAll(
            runner.TryInferAsync(job),
            runner.TryInferAsync(jobTwo));

        Assert.Equal(2, client.CallCount);
        Assert.All(results, Assert.NotNull);
    }

    [Fact]
    public async Task TryInferAsync_ReturnsNullWhenClientReturnsUnrepairablePayload()
    {
        var client = new StubTriageLlmClient("Sure, I can help but I have no JSON today.");
        var runner = new MessageTriageInferenceRunner(client);

        var result = await runner.TryInferAsync(CreateJob("broken"));

        Assert.Null(result);
    }

    [Fact]
    public async Task TryInferAsync_RepairsClippedStreamFromClient()
    {
        const string clipped = """
            Here is the analysis:
            {"UrgencyScore":91,"Sentiment":"Negative","CustomerIntent":"Complaint","ExtractedEntities":{"CustomerName":"Sara","ContactNumber":null,"RequestedDate":null,"RequestedTime":null,"ServiceType":null,"ActionRequired":"call"},"CoreSummary":"Urgent cancel request
            """;
        var client = new StubTriageLlmClient(clipped);
        var runner = new MessageTriageInferenceRunner(client);

        var result = await runner.TryInferAsync(CreateJob("clip"));

        Assert.NotNull(result);
        Assert.Equal(91, result!.UrgencyScore);
    }

    [Fact]
    public void ApplyInference_UpgradesBaselineItem()
    {
        var baseline = new MessageTriageItem
        {
            Id = "inst|abc",
            InstanceId = "inst",
            InstanceDisplayName = "Branch",
            Platform = "metabusiness",
            MessagePreview = "Refund now",
            UrgencyScore = 45,
            Sentiment = MessageSentiment.Neutral,
            InferenceSource = TriageInferenceSource.Heuristic
        };

        var enriched = MessageTriageInferenceRunner.ApplyInference(
            baseline,
            new RichTriageLlmResponse
            {
                UrgencyScore = 92,
                Sentiment = "Negative",
                CustomerIntent = "Complaint",
                CoreSummary = "Customer demands immediate refund today please",
                ExtractedEntities = new RichTriageExtractedEntities
                {
                    CustomerName = "Sara",
                    ActionRequired = "call back"
                }
            });

        Assert.Equal(TriageInferenceSource.LocalAi, enriched.InferenceSource);
        Assert.Equal(92, enriched.UrgencyScore);
        Assert.Equal(MessageSentiment.Negative, enriched.Sentiment);
        Assert.Equal(CustomerIntent.Complaint, enriched.CustomerIntent);
        Assert.Equal("Customer demands immediate refund today please", enriched.CoreSummary);
        Assert.Equal("Sara", enriched.ExtractedEntities.CustomerName);
    }

    private static RichTriageInferenceJob CreateJob(string triageItemId) =>
        new()
        {
            TriageItemId = triageItemId,
            InstanceId = "branch-1",
            InstanceDisplayName = "Depilex F-11",
            Platform = "metabusiness",
            MessageText = "I need to cancel immediately.",
            HeuristicUrgencyScore = 70,
            HeuristicSentiment = MessageSentiment.Negative
        };

    private sealed class StubTriageLlmClient(string payload) : ITriageLlmClient
    {
        public Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken) =>
            Task.FromResult<string?>(payload);
    }

    private sealed class CountingTriageLlmClient(TimeSpan delay) : ITriageLlmClient
    {
        private int _active;
        private readonly object _gate = new();

        public int MaxConcurrentObserved { get; private set; }

        public int CallCount { get; private set; }

        public async Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                CallCount++;
                _active++;
                if (_active > MaxConcurrentObserved)
                {
                    MaxConcurrentObserved = _active;
                }
            }

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                return """
                    {"UrgencyScore":50,"Sentiment":"Neutral","CustomerIntent":"Inquiry","ExtractedEntities":{"CustomerName":null,"ContactNumber":null,"RequestedDate":null,"RequestedTime":null,"ServiceType":null,"ActionRequired":null},"CoreSummary":"Neutral inquiry"}
                    """;
            }
            finally
            {
                lock (_gate)
                {
                    _active--;
                }
            }
        }
    }
}
