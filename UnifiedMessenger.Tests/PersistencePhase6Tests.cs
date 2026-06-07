using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class PersistencePhase6Tests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public PersistencePhase6Tests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, RichTriageStoreService.FileName);
    }

    [Fact]
    public async Task LoadAsync_MarksStoreLoadedForEmptyFile()
    {
        var store = new RichTriageStoreService(_storePath);
        var result = await store.LoadAsync();

        Assert.True(store.IsLoaded);
        Assert.Equal(RichTriageStoreLoadStatus.CreatedEmpty, result.Status);
    }

    [Fact]
    public void PruneStoreSnapshot_DropsItemsWithoutMatchingThreads()
    {
        var items = new[]
        {
            CreateItem("inst", "a", threadId: "inst|chat-a"),
            CreateItem("inst", "b", threadId: "inst|orphan")
        };

        var threads = new[]
        {
            new ThreadData
            {
                ThreadId = "inst|chat-a",
                Platform = "whatsapp",
                InstanceId = "inst",
                LastMessageTime = DateTimeOffset.UtcNow
            }
        };

        var pruned = RichTriageStoreService.PruneStoreSnapshot(items, threads);

        Assert.Single(pruned.Items);
        Assert.Equal("inst|chat-a", pruned.Items[0].ThreadId);
        Assert.Single(pruned.Threads);
    }

    [Fact]
    public void BackfillThreadsFromItems_SetsFirstInboundFromOldestItem()
    {
        var oldest = DateTimeOffset.UtcNow.AddHours(-3);
        var newest = DateTimeOffset.UtcNow.AddMinutes(-5);
        var store = new RichTriageStoreFile
        {
            Version = 1,
            Items =
            [
                CreateItem("inst", "Sara", "923001234567@c.us", newest, "inst|923001234567@c.us"),
                CreateItem("inst", "Sara", "923001234567@c.us", oldest, "inst|923001234567@c.us")
            ]
        };

        var migrated = RichTriageStoreMigrator.Migrate(store);
        var thread = Assert.Single(migrated.Threads);

        Assert.Equal(oldest, thread.FirstInboundAtUtc);
        Assert.Equal(newest, thread.LastMessageTime);
    }

    [Fact]
    public void RepairFirstInboundAtUtc_UsesOldestMatchingItem()
    {
        var firstAt = DateTimeOffset.UtcNow.AddHours(-4);
        var store = new RichTriageStoreFile
        {
            Version = 2,
            Items =
            [
                CreateItem("inst", "Sara", "923001234567@c.us", firstAt, "inst|923001234567@c.us")
            ],
            Threads =
            [
                new ThreadData
                {
                    ThreadId = "inst|923001234567@c.us",
                    Platform = "whatsapp",
                    InstanceId = "inst",
                    LastMessageTime = DateTimeOffset.UtcNow,
                    FirstInboundAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };

        RichTriageStoreMigrator.RepairFirstInboundAtUtc(store);

        Assert.Equal(firstAt, store.Threads[0].FirstInboundAtUtc);
    }

    [Fact]
    public async Task LoadAsync_RecoversFromCorruptJsonWithResult()
    {
        await File.WriteAllTextAsync(_storePath, "{ not valid triage json");

        var store = new RichTriageStoreService(_storePath);
        var result = await store.LoadAsync();

        Assert.True(store.IsLoaded);
        Assert.Equal(RichTriageStoreLoadStatus.CorruptRecovered, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.NotEmpty(Directory.GetFiles(_tempDirectory, "triage_v2.json.corrupt-*.bak"));
    }

    [Fact]
    public void MessageTriageService_ShutdownCompletesWorker()
    {
        var triage = new MessageTriageService();
        triage.Shutdown();
        Assert.True(triage.WaitForShutdownAsync(TimeSpan.FromSeconds(1)).IsCompletedSuccessfully);
    }

    private static MessageTriageItem CreateItem(
        string instanceId,
        string customer,
        string conversationKey,
        DateTimeOffset timestampUtc,
        string threadId) =>
        new()
        {
            Id = $"{instanceId}|{Guid.NewGuid():N}",
            InstanceId = instanceId,
            InstanceDisplayName = instanceId,
            Platform = "whatsappbusiness",
            MessagePreview = "Need help",
            CustomerName = customer,
            ConversationKey = conversationKey,
            ThreadId = threadId,
            UrgencyScore = 40,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = timestampUtc
        };

    private static MessageTriageItem CreateItem(
        string instanceId,
        string customer,
        string threadId) =>
        CreateItem(
            instanceId,
            customer,
            customer,
            DateTimeOffset.UtcNow,
            threadId);

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
