using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

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
