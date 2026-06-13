using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Acceptance scenarios for WhatsApp resolve policy (Plan v4 Wave B).
/// </summary>
[Collection(UnifiedMessengerSerialCollection.Name)]
public class ZohraResolvePolicyAcceptanceTests : IDisposable
{
    private const string InstanceId = "zohra-wa";
    private const string Jid = "923001234567@c.us";
    private const string DisplayName = "Zohra";

    private readonly IReadOnlyList<ThreadData> _originalThreads;

    public ZohraResolvePolicyAcceptanceTests()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
    }

    public void Dispose() => ThreadRegistryService.Instance.RestoreThreads(_originalThreads);

    [Fact]
    public async Task ScenarioA_WebReplyMarksResolvedWithCorrectFrt()
    {
        var inboundAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var replyAt = inboundAt.AddMinutes(12);

        SeedOpenThread(Jid, DisplayName, inboundAt);

        DispatchMessageSent(DisplayName, DisplayName, replyAt);

        var thread = await WaitForResolvedThreadAsync(Jid);
        Assert.Equal(12, thread.ReplyLatencyMinutes, precision: 0);
        Assert.Equal(12, thread.LatencyMinutes, precision: 0);
    }

    [Fact]
    public async Task ScenarioB_PhoneReplyOnOpenResolvesThreadViaDisplayNameAlias()
    {
        var inboundAt = new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
        var resolvedAt = inboundAt.AddMinutes(8);

        SeedOpenThread(Jid, DisplayName, inboundAt);

        DispatchThreadResolved(DisplayName, DisplayName, resolvedAt);

        var thread = await WaitForResolvedThreadAsync(Jid);
        Assert.Equal(8, thread.ReplyLatencyMinutes, precision: 0);
    }

    [Fact]
    public async Task ScenarioC_OpenChatWithOutgoingLastResolvesUnrepliedThread()
    {
        var inboundAt = new DateTimeOffset(2026, 6, 3, 14, 30, 0, TimeSpan.Zero);
        var resolvedAt = inboundAt.AddMinutes(5);

        SeedOpenThread(Jid, DisplayName, inboundAt);

        DispatchThreadResolved(Jid, DisplayName, resolvedAt);

        var thread = await WaitForResolvedThreadAsync(Jid);
        Assert.True(thread.IsReplied);
        Assert.Equal(5, thread.ReplyLatencyMinutes, precision: 0);
    }

    [Fact]
    public async Task ScenarioD_DisconnectedInstanceResolveDoesNotCreatePhantomThread()
    {
        InstanceConnectionStatusService.Instance.SetError(InstanceId, "disconnected");

        DispatchThreadResolved(DisplayName, DisplayName, DateTimeOffset.UtcNow);

        await Task.Delay(100);

        Assert.Empty(ThreadRegistryService.Instance.GetAllThreads());
    }

    [Fact]
    public void ScenarioD_WhatsAppMessageSentWithoutOpenThread_DoesNotResolve()
    {
        DispatchMessageSent(DisplayName, DisplayName, DateTimeOffset.UtcNow);

        Assert.Empty(ThreadRegistryService.Instance.GetAllThreads());
    }

    private static void SeedOpenThread(string conversationKey, string customerName, DateTimeOffset inboundAt)
    {
        var item = CreateItem(customerName, inboundAt);
        ThreadRegistryService.Instance.UpsertFromTriageItem(item, conversationKey, "DHA-2");
    }

    private static void DispatchThreadResolved(
        string conversationKey,
        string customerName,
        DateTimeOffset resolvedAtUtc)
    {
        var payload = $$"""
            {
              "type": "update-thread-status",
              "instanceId": "{{InstanceId}}",
              "status": "RESOLVED",
              "conversationKey": "{{conversationKey}}",
              "customerName": "{{customerName}}",
              "source": "thread-status-auditor",
              "timestampUtc": "{{resolvedAtUtc:O}}"
            }
            """;

        DispatchPayload(payload);
    }

    private static void DispatchMessageSent(
        string conversationKey,
        string chatHint,
        DateTimeOffset sentAtUtc)
    {
        var payload = $$"""
            {
              "type": "message-sent",
              "instanceId": "{{InstanceId}}",
              "platform": "whatsapp",
              "source": "enter-key",
              "chatHint": "{{chatHint}}",
              "conversationKey": "{{conversationKey}}",
              "timestampUtc": "{{sentAtUtc:O}}"
            }
            """;

        DispatchPayload(payload);
    }

    private static void DispatchPayload(string json)
    {
        var instance = new MessengerInstance
        {
            Id = InstanceId,
            DisplayName = "Depilex DHA-2",
            Platform = "whatsapp",
            ProfileName = InstanceId,
            StartUrl = "https://web.whatsapp.com/"
        };

        var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
        adapter.HandleWebMessage(json, NotificationHub.Instance, instance);
    }

    private static async Task<ThreadData> WaitForResolvedThreadAsync(string conversationKey)
    {
        var threadId = ConversationKeyResolver.BuildThreadId(InstanceId, conversationKey);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var thread = ThreadRegistryService.Instance.GetAllThreads()
                .FirstOrDefault(t => t.ThreadId == threadId);
            if (thread?.IsReplied == true)
            {
                return thread;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Thread '{threadId}' was not marked resolved.");
    }

    private static MessageTriageItem CreateItem(string customer, DateTimeOffset timestampUtc) =>
        new()
        {
            Id = $"{InstanceId}|seed",
            InstanceId = InstanceId,
            InstanceDisplayName = "Depilex DHA-2",
            Platform = "whatsapp",
            MessagePreview = "Need bridal quote for Saturday",
            CustomerName = customer,
            UrgencyScore = 45,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = timestampUtc,
            AiIntentCategory = UnifiedMessengerIntentCategory.PriceInquiry
        };
}
