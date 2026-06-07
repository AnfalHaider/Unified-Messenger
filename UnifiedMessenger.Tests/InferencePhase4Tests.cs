using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class InferencePhase4Tests
{
    [Fact]
    public async Task TryInferAsync_UsesSeparateRetryTimeout()
    {
        var client = new RetryTimingTriageLlmClient();
        var runner = new MessageTriageInferenceRunner(client);

        var result = await runner.TryInferAsync(CreateJob());

        Assert.NotNull(result);
        Assert.Equal(2, client.CallCount);
        Assert.True(client.SecondCallHadFreshBudget);
    }

    [Fact]
    public async Task TryInferAsync_ReturnsNullWhenCallerCancelledDuringSlowCall()
    {
        using var cts = new CancellationTokenSource();
        var client = new SlowTriageLlmClient(TimeSpan.FromSeconds(5));
        var runner = new MessageTriageInferenceRunner(client);
        var task = runner.TryInferAsync(CreateJob(), cts.Token);
        cts.CancelAfter(25);

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(task, completed);
        Assert.Null(await task);
    }

    [Fact]
    public void ProcessInbound_StoresFullTextForInsights()
    {
        var longMessage = new string('a', 250) + " urgent bridal booking";
        var service = new MessageTriageService();

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "inst",
                Platform = "whatsappbusiness",
                MessageText = longMessage
            },
            "Branch");

        var item = Assert.Single(service.GetAllItems());
        Assert.True(item.MessagePreview.Length <= 220);
        Assert.Equal(longMessage, item.MessageFullText);
    }

    [Fact]
    public void BuildSnapshot_UrgentQueueUsesOperationalUrgency()
    {
        var service = new MessageTriageService();
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "inst",
                DisplayName = "Branch",
                ProfileName = "inst",
                Platform = "metabusiness",
                StartUrl = "https://example.com",
                Category = WorkspaceCategory.Professional
            }
        };

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "inst",
                Platform = "metabusiness",
                MessageText = "Thanks for the great service yesterday!"
            },
            "Branch");

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "inst",
                Platform = "metabusiness",
                MessageText = "I need to cancel my booking immediately, this is urgent."
            },
            "Branch");

        var snapshot = service.BuildSnapshot(instances);

        Assert.Single(snapshot.UrgentQueue);
        Assert.True(snapshot.UrgentQueue[0].OperationalUrgency >= 4);
    }

    [Fact]
    public void BuildWeeklySentiment_CountsCriticalClientSentimentAsNegative()
    {
        var service = new MessageTriageService();
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "inst",
                DisplayName = "Branch",
                ProfileName = "inst",
                Platform = "metabusiness",
                StartUrl = "https://example.com",
                Category = WorkspaceCategory.Professional
            }
        };

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "inst",
                Platform = "metabusiness",
                MessageText = "Need help with my order"
            },
            "Branch");

        var item = service.GetAllItems()[0];
        service.ReplaceItemForInsights(new MessageTriageItem
        {
            Id = item.Id,
            InstanceId = item.InstanceId,
            InstanceDisplayName = item.InstanceDisplayName,
            Platform = item.Platform,
            MessagePreview = item.MessagePreview,
            MessageFullText = item.MessageFullText,
            CustomerName = item.CustomerName,
            UrgencyScore = item.UrgencyScore,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            ClientSentiment = ClientSentimentLabel.Critical,
            OperationalUrgency = 5
        });

        var today = service.BuildSnapshot(instances).WeeklySentiment.Last();
        Assert.Equal(1, today.Negative);
        Assert.Equal(0, today.Neutral);
    }

    private static RichTriageInferenceJob CreateJob() =>
        new()
        {
            TriageItemId = "inst|1",
            InstanceId = "inst",
            InstanceDisplayName = "Branch",
            Platform = "metabusiness",
            MessageText = "Cancel immediately",
            HeuristicUrgencyScore = 80,
            HeuristicSentiment = MessageSentiment.Negative
        };

    private sealed class RetryTimingTriageLlmClient : ITriageLlmClient
    {
        public int CallCount { get; private set; }

        public bool SecondCallHadFreshBudget { get; private set; }

        public async Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken,
            bool strictJsonRetry = false)
        {
            CallCount++;
            if (!strictJsonRetry)
            {
                return "not json";
            }

            SecondCallHadFreshBudget = !cancellationToken.IsCancellationRequested;
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            return """
                {"isSpamOrPromo":false,"intentCategory":"Complaint","urgencyScore":5,"actionableSummary":"Cancel now","suggestedAction":"Call Client"}
                """;
        }
    }

    private sealed class SlowTriageLlmClient(TimeSpan delay) : ITriageLlmClient
    {
        public async Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken,
            bool strictJsonRetry = false)
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            return """
                {"isSpamOrPromo":false,"intentCategory":"Complaint","urgencyScore":5,"actionableSummary":"Cancel now","suggestedAction":"Call Client"}
                """;
        }
    }
}
