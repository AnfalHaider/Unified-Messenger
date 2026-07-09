using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ai;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Tests.Ai;

public class AiInferenceQueueTests
{
    [Fact]
    public void EnqueueIfEligible_Backfill_DisallowsLlm()
    {
        var (queue, _, _, _) = CreateQueue();
        var item = BuildItem(urgency: 90);

        var enqueued = queue.EnqueueIfEligible(item, allowLlmInference: false);

        Assert.False(enqueued);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void EnqueueIfEligible_Spam_IsSkipped()
    {
        var (queue, _, _, _) = CreateQueue();
        var item = BuildItem(urgency: 90, isSpam: true);

        var enqueued = queue.EnqueueIfEligible(item, allowLlmInference: true);

        Assert.False(enqueued);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void EnqueueIfEligible_CoalescesByThreadId()
    {
        var (queue, registry, _, _) = CreateQueue();
        var first = BuildItem(threadId: "inst|chat-1", triageId: "a", urgency: 40);
        var second = BuildItem(threadId: "inst|chat-1", triageId: "b", urgency: 95);
        registry.UpsertFromTriageItem(first, first.ConversationKey, first.BranchName);

        Assert.True(queue.EnqueueIfEligible(first, allowLlmInference: true));
        Assert.True(queue.EnqueueIfEligible(second, allowLlmInference: true));

        Assert.Equal(1, queue.PendingCount);
        var selected = queue.SelectEligibleThreadIdsForTests();
        Assert.Single(selected);
        Assert.Equal("inst|chat-1", selected[0]);
    }

    [Fact]
    public void SelectEligibleThreadIds_RespectsTopFiveCap()
    {
        var (queue, registry, _, _) = CreateQueue();
        for (var i = 0; i < 8; i++)
        {
            var item = BuildItem(
                threadId: $"inst|chat-{i}",
                conversationKey: $"chat-{i}",
                triageId: $"id-{i}",
                urgency: 10 + i);
            registry.UpsertFromTriageItem(item, item.ConversationKey, item.BranchName);
            queue.EnqueueIfEligible(item, allowLlmInference: true);
        }

        var selected = queue.SelectEligibleThreadIdsForTests(topN: 5);

        Assert.Equal(5, selected.Count);
        Assert.Equal("inst|chat-7", selected[0]);
        Assert.Equal("inst|chat-3", selected[4]);
    }

    [Fact]
    public void CancelThread_RemovesPendingWork()
    {
        var (queue, registry, _, _) = CreateQueue();
        var item = BuildItem(threadId: "inst|chat-1", urgency: 90);
        registry.UpsertFromTriageItem(item, item.ConversationKey, item.BranchName);

        Assert.True(queue.EnqueueIfEligible(item, allowLlmInference: true));
        queue.CancelThread(item.ThreadId);

        Assert.Equal(0, queue.PendingCount);
        Assert.Empty(queue.SelectEligibleThreadIdsForTests());
    }

    [Fact]
    public async Task ProcessNextEligibleAsync_EnrichesThreadAndTriageItem()
    {
        var (queue, registry, triage, client) = CreateQueue();
        var item = BuildItem(threadId: "inst|chat-1", triageId: "triage-1", urgency: 90);
        registry.UpsertFromTriageItem(item, item.ConversationKey, item.BranchName);
        triage.ResetForTests([item]);
        queue.EnqueueIfEligible(item, allowLlmInference: true);

        await queue.ProcessNextEligibleAsync();

        var thread = registry.GetAllThreads().Single();
        Assert.Equal(UnifiedMessengerIntentCategory.Complaint, thread.AiIntentCategory);
        Assert.Equal("Apologize and offer resolution", thread.NextActionSummary);

        var enriched = triage.GetAllItems().Single();
        Assert.Equal(TriageInferenceSource.Ollama, enriched.InferenceSource);
        Assert.Equal(1, client.GenerateCallCount);
    }

    private static (AiInferenceQueue Queue, ThreadRegistryService Registry, MessageTriageService Triage, FakeAiInferenceClient Client) CreateQueue()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var client = new FakeAiInferenceClient
        {
            PingHandler = _ => true,
            GenerateHandler = (_, _, _) => new AiInferenceResult
            {
                Intent = UnifiedMessengerIntentCategory.Complaint,
                NextAction = "Apologize and offer resolution",
                SuggestedAction = "Escalate"
            }
        };

        var runtime = new OllamaRuntimeService(
            inferenceClient: client,
            settingsProvider: () => new AppSettings
            {
                EnableLocalAi = true,
                LocalAiModelName = "phi3:mini",
                OllamaAutoBootstrap = false
            },
            downloadClient: new HttpClient(),
            runtimeInstallDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            embeddedExecutablePath: Path.Combine(Path.GetTempPath(), "missing", "ollama.exe"),
            modelsDirectory: Path.Combine(Path.GetTempPath(), "missing", "models"));

        runtime.EnsureRunningAsync().GetAwaiter().GetResult();

        MessageTriageService? triageHolder = null;
        var queue = new AiInferenceQueue(
            startBackgroundWorker: false,
            inferenceClient: client,
            runtimeService: runtime,
            settingsProvider: () => new AppSettings { EnableLocalAi = true, LocalAiModelName = "phi3:mini" },
            threadRegistry: registry,
            messageTriageProvider: () => triageHolder!);

        triageHolder = new MessageTriageService(queue);
        return (queue, registry, triageHolder, client);
    }

    private static MessageTriageItem BuildItem(
        string threadId = "inst|chat-1",
        string conversationKey = "chat-1",
        string triageId = "triage-1",
        int urgency = 50,
        bool isSpam = false)
    {
        return new MessageTriageItem
        {
            Id = triageId,
            InstanceId = "inst",
            InstanceDisplayName = "Branch",
            Platform = "whatsapp",
            MessagePreview = "Need help with my booking",
            MessageFullText = "Need help with my booking",
            CustomerName = "Sara",
            UrgencyScore = urgency,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            ThreadId = threadId,
            ConversationKey = conversationKey,
            BranchName = "General",
            OperationalUrgency = ThreadRegistryService.MapOperationalUrgency(urgency),
            IsSpamOrPromo = isSpam
        };
    }
}
