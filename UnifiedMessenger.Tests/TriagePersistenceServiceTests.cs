using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public sealed class TriagePersistenceServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storePath;
    private readonly TriagePersistenceService _service;

    public TriagePersistenceServiceTests()
    {
        _storePath = Path.Combine(Path.GetTempPath(), $"um-triage-{Guid.NewGuid():N}.json");
        _service = new TriagePersistenceService(_storePath);
        _service.SuppressPersistence = true;
        TriagePersistenceService.Instance.SuppressPersistence = true;
        ThreadDisplayOrderService.Instance.SuppressPersistence = true;
    }

    public void Dispose()
    {
        TriagePersistenceService.Instance.SuppressPersistence = false;
        ThreadDisplayOrderService.Instance.SuppressPersistence = false;

        if (File.Exists(_storePath))
        {
            File.Delete(_storePath);
        }
    }

    [Fact]
    public async Task RoundTrip_PersistsTriageThreadsAndDisplayOrder()
    {
        var triageItem = new MessageTriageItem
        {
            Id = "inst-1|abc",
            InstanceId = "inst-1",
            InstanceDisplayName = "Sales",
            Platform = "whatsapp",
            MessagePreview = "Need pricing",
            CustomerName = "Alex",
            UrgencyScore = 40,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            ThreadId = "inst-1|chat-1",
            ConversationKey = "chat-1",
            BranchName = "Main"
        };

        var thread = new ThreadData
        {
            ThreadId = "inst-1|chat-1",
            Platform = "whatsapp",
            InstanceId = "inst-1",
            InstanceDisplayName = "Sales",
            CustomerName = "Alex",
            ConversationKey = "chat-1",
            BranchName = "Main",
            UrgencyScore = 3,
            LastMessageTime = DateTimeOffset.UtcNow
        };

        MessageTriageService.Instance.RestoreItems([triageItem]);
        ThreadRegistryService.Instance.RestoreThreads([thread]);
        ThreadDisplayOrderService.Instance.UpdateColumnOrder(
            ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries),
            ["inst-1|chat-1"]);

        var snapshot = _service.BuildStoreSnapshot();
        await File.WriteAllTextAsync(
            _storePath,
            JsonSerializer.Serialize(snapshot, JsonOptions));

        MessageTriageService.Instance.RestoreItems([]);
        ThreadRegistryService.Instance.RestoreThreads([]);
        ThreadDisplayOrderService.Instance.ResetForTests();

        await _service.LoadAsync();

        var restoredTriage = MessageTriageService.Instance.GetAllItems();
        Assert.Single(restoredTriage);
        Assert.Equal("Need pricing", restoredTriage[0].MessagePreview);

        var restoredThreads = ThreadRegistryService.Instance.GetAllThreads();
        Assert.Single(restoredThreads);
        Assert.Equal("Alex", restoredThreads[0].CustomerName);

        var sortIndex = ThreadDisplayOrderService.Instance.GetSortIndex(
            ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries),
            "inst-1|chat-1");
        Assert.Equal(0, sortIndex);
    }
}
