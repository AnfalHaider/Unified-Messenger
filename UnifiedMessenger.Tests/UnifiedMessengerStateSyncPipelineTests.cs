using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class UnifiedMessengerStateSyncServiceTests : IDisposable
{
    private readonly IReadOnlyList<ThreadData> _originalThreads;

    public UnifiedMessengerStateSyncServiceTests()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
    }

    public void Dispose() => ThreadRegistryService.Instance.RestoreThreads(_originalThreads);

    [Fact]
    public async Task ProcessEvent_ThreadResolved_MarksRegistry()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateItem("sync-1", "Noor"),
            "Noor",
            "F-11");

        var sync = new UnifiedMessengerStateSyncService();
        await sync.ProcessEventForTestsAsync(new UnifiedMessengerSyncEvent
        {
            Kind = UnifiedMessengerSyncEventKind.ThreadResolved,
            InstanceId = "sync-1",
            ConversationKey = "Noor",
            CustomerName = "Noor",
            Source = "thread-status-auditor"
        });

        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .Single(t => t.ThreadId == "sync-1|Noor");
        Assert.True(thread.IsReplied);
    }

    [Fact]
    public async Task EnqueueThreadResolved_ProcessedByWorker()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateItem("sync-2", "Aisha"),
            "Aisha",
            "DHA-2");

        UnifiedMessengerStateSyncService.Instance.EnqueueThreadResolved(
            "sync-2",
            "Aisha",
            "Aisha",
            source: "test");

        await WaitForConditionAsync(
            () => ThreadRegistryService.Instance.GetAllThreads()
                .Any(t => t.ThreadId == "sync-2|Aisha" && t.IsReplied),
            TimeSpan.FromSeconds(2));

        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .Single(t => t.ThreadId == "sync-2|Aisha");
        Assert.True(thread.IsReplied);
    }

    private static MessageTriageItem CreateItem(string instanceId, string customer) =>
        new()
        {
            Id = $"{instanceId}|seed",
            InstanceId = instanceId,
            InstanceDisplayName = "Depilex F-11",
            Platform = "whatsapp",
            MessagePreview = "Need appointment",
            CustomerName = customer,
            UrgencyScore = 30,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow
        };

    private static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }
}

public class RichTriageStoreMigratorTests
{
    [Fact]
    public void Migrate_V1ItemsOnly_BackfillsThreadsAndBranchCatalog()
    {
        var store = new RichTriageStoreFile
        {
            Version = 1,
            Items =
            [
                new MessageTriageItem
                {
                    Id = "dha|1",
                    InstanceId = "dha",
                    InstanceDisplayName = "Depilex DHA-2",
                    Platform = "metabusiness",
                    MessagePreview = "Booking inquiry",
                    CustomerName = "Sara",
                    UrgencyScore = 40,
                    Sentiment = MessageSentiment.Neutral,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    BranchName = "DHA-2"
                },
                new MessageTriageItem
                {
                    Id = "f11|1",
                    InstanceId = "f11",
                    InstanceDisplayName = "Depilex F-11",
                    Platform = "googlebusiness",
                    MessagePreview = "Price question",
                    CustomerName = "Ali",
                    UrgencyScore = 25,
                    Sentiment = MessageSentiment.Neutral,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    BranchName = "F-11"
                }
            ]
        };

        var migrated = RichTriageStoreMigrator.Migrate(store);

        Assert.Equal(RichTriageStoreFile.CurrentVersion, migrated.Version);
        Assert.Equal(2, migrated.Threads.Count);
        Assert.Equal(2, migrated.Metadata.Branches.Count);
        Assert.Contains(migrated.Metadata.Branches, b => b.BranchName == "DHA-2" && b.Platform == "metabusiness");
        Assert.Contains(migrated.Metadata.Branches, b => b.BranchName == "F-11" && b.Platform == "googlebusiness");
    }

    [Fact]
    public void BuildBranchCatalog_DeduplicatesByBranchPlatformInstance()
    {
        var items = new[]
        {
            new MessageTriageItem
            {
                Id = "a|1",
                InstanceId = "a",
                InstanceDisplayName = "IgnitePro",
                Platform = "whatsappbusiness",
                MessagePreview = "m1",
                CustomerName = "One",
                UrgencyScore = 10,
                Sentiment = MessageSentiment.Neutral,
                TimestampUtc = DateTimeOffset.UtcNow,
                BranchName = "IgnitePro"
            },
            new MessageTriageItem
            {
                Id = "a|2",
                InstanceId = "a",
                InstanceDisplayName = "IgnitePro",
                Platform = "whatsappbusiness",
                MessagePreview = "m2",
                CustomerName = "Two",
                UrgencyScore = 10,
                Sentiment = MessageSentiment.Neutral,
                TimestampUtc = DateTimeOffset.UtcNow,
                BranchName = "IgnitePro"
            }
        };

        var catalog = RichTriageStoreMigrator.BuildBranchCatalog(items, []);
        Assert.Single(catalog);
        Assert.Equal("IgnitePro", catalog[0].BranchName);
        Assert.Equal("whatsappbusiness", catalog[0].Platform);
    }
}

[Collection(UnifiedMessengerSerialCollection.Name)]
public class TriageInferencePriorityBrokerTests
{
    [Fact]
    public async Task WaitForBackgroundSlotAsync_BlocksWhileInteractiveActive()
    {
        OllamaInferenceCoordinator.Instance.SetActivityForTests(OllamaInferenceActivity.InteractiveStreaming);

        var waitTask = TriageInferencePriorityBroker.WaitForBackgroundSlotAsync(
            new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

        await Task.Delay(100);
        Assert.False(waitTask.IsCompleted);

        OllamaInferenceCoordinator.Instance.SetActivityForTests(OllamaInferenceActivity.Idle);
        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ShouldDeferBackgroundInference_IsTrueDuringInteractive()
    {
        try
        {
            OllamaInferenceCoordinator.Instance.SetActivityForTests(OllamaInferenceActivity.InteractiveStreaming);
            Assert.True(TriageInferencePriorityBroker.ShouldDeferBackgroundInference);
        }
        finally
        {
            OllamaInferenceCoordinator.Instance.SetActivityForTests(OllamaInferenceActivity.Idle);
        }
    }

    [Fact]
    public void ShouldDeferBackgroundInference_IsFalseWhenIdle()
    {
        OllamaInferenceCoordinator.Instance.SetActivityForTests(OllamaInferenceActivity.Idle);
        Assert.False(TriageInferencePriorityBroker.ShouldDeferBackgroundInference);
    }
}
