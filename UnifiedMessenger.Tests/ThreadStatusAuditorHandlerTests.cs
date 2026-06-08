using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class ThreadStatusAuditorHandlerTests : IDisposable
{
    private readonly IReadOnlyList<ThreadData> _originalThreads;

    public ThreadStatusAuditorHandlerTests()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
    }

    public void Dispose() => ThreadRegistryService.Instance.RestoreThreads(_originalThreads);

    [Fact]
    public async Task UpdateThreadStatus_ResolvedPayload_MarksThreadReplied()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateOpenThread("wa-branch", "Sara"),
            "Sara",
            "DHA-2");

        var payload = """
            {
              "type": "update-thread-status",
              "instanceId": "wa-branch",
              "status": "RESOLVED",
              "conversationKey": "Sara",
              "customerName": "Sara",
              "source": "thread-status-auditor",
              "timestamp": "2026-06-03T12:00:00Z"
            }
            """;

        DispatchPayload(payload, "wa-branch");

        await WaitForThreadResolvedAsync("wa-branch|Sara");

        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .Single(t => t.ThreadId == "wa-branch|Sara");
        Assert.True(thread.IsReplied);
        Assert.False(thread.IsRevenueLeakageRisk);
    }

    [Fact]
    public void UpdateThreadStatus_NonResolvedStatus_IsIgnored()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateOpenThread("wa-branch-2", "Sara"),
            "Sara",
            "DHA-2");

        var payload = """
            {
              "type": "update-thread-status",
              "instanceId": "wa-branch-2",
              "status": "OPEN",
              "conversationKey": "Sara",
              "customerName": "Sara"
            }
            """;

        DispatchPayload(payload, "wa-branch-2");

        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .Single(t => t.ThreadId == "wa-branch-2|Sara");
        Assert.False(thread.IsReplied);
    }

    [Fact]
    public void UpdateThreadStatus_MismatchedInstanceId_IsIgnored()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateOpenThread("wa-branch-3", "Sara"),
            "Sara",
            "DHA-2");

        var payload = """
            {
              "type": "update-thread-status",
              "instanceId": "other-instance",
              "status": "RESOLVED",
              "conversationKey": "Sara",
              "customerName": "Sara"
            }
            """;

        DispatchPayload(payload, "wa-branch-3");

        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .Single(t => t.ThreadId == "wa-branch-3|Sara");
        Assert.False(thread.IsReplied);
    }

    [Fact]
    public void AdapterMessageTypes_RecognizesUpdateThreadStatus()
    {
        Assert.True(AdapterMessageTypes.IsKnownType(AdapterMessageTypes.UpdateThreadStatus));
    }

    private static async Task WaitForThreadResolvedAsync(string threadId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var thread = ThreadRegistryService.Instance.GetAllThreads()
                .FirstOrDefault(t => t.ThreadId == threadId);
            if (thread?.IsReplied == true)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Thread '{threadId}' was not marked resolved.");
    }

    private static void DispatchPayload(string json, string instanceId = "wa-branch")
    {
        var instance = new MessengerInstance
        {
            Id = instanceId,
            DisplayName = "Depilex DHA-2",
            Platform = "whatsappbusiness",
            ProfileName = instanceId,
            StartUrl = "https://web.whatsapp.com/"
        };

        var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
        adapter.HandleWebMessage(json, NotificationHub.Instance, instance);
    }

    private static MessageTriageItem CreateOpenThread(string instanceId, string customer) =>
        new()
        {
            Id = $"{instanceId}|seed",
            InstanceId = instanceId,
            InstanceDisplayName = "Depilex DHA-2",
            Platform = "whatsappbusiness",
            MessagePreview = "Need quote for bridal package",
            CustomerName = customer,
            UrgencyScore = 45,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow,
            AiIntentCategory = UnifiedMessengerIntentCategory.PriceInquiry
        };
}
