using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ConversationKeyResolverTests
{
    [Fact]
    public void BuildReviewKey_FormatsGoogleReviewId()
    {
        Assert.Equal("review:abc123", ConversationKeyResolver.BuildReviewKey("abc123"));
    }

    [Fact]
    public void Resolve_PrefersExplicitWhatsAppJid()
    {
        const string jid = "923001234567@c.us";
        var key = ConversationKeyResolver.Resolve("whatsappbusiness", jid, "Sidebar Title", "Customer");
        Assert.Equal(jid, key);
    }

    [Fact]
    public void Resolve_PrefersReviewPrefixFromHint()
    {
        var key = ConversationKeyResolver.Resolve(
            "googlebusiness",
            conversationHint: "review:rev-42",
            customerName: "Ali");
        Assert.Equal("review:rev-42", key);
    }

    [Fact]
    public void BuildThreadId_UsesNormalizedKey()
    {
        Assert.Equal("inst-1|review:rev-9", ConversationKeyResolver.BuildThreadId("inst-1", "review:rev-9"));
    }

    [Fact]
    public void IsGenericConversationLabel_TreatsMetaInboxChromeAsGeneric()
    {
        Assert.True(ConversationKeyResolver.IsGenericConversationLabel("Inbox", "metabusiness"));
        Assert.True(ConversationKeyResolver.IsGenericConversationLabel("Meta Business Suite", "metabusiness"));
        Assert.False(ConversationKeyResolver.IsGenericConversationLabel("Sara Khan", "metabusiness"));
    }

    [Fact]
    public void Resolve_MetaBusiness_RejectsGenericHeaderAndUsesMessageFingerprint()
    {
        var key = ConversationKeyResolver.Resolve(
            "metabusiness",
            conversationKey: "Inbox",
            customerName: "Inbox",
            messagePreview: "Need bridal makeup pricing for Saturday");

        Assert.StartsWith(ConversationKeyResolver.MetaMessageKeyPrefix, key, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_MetaBusiness_UsesParticipantNameWhenProvided()
    {
        var key = ConversationKeyResolver.Resolve(
            "metabusiness",
            conversationKey: "Sara Khan",
            customerName: "Sara Khan",
            messagePreview: "Need bridal makeup pricing for Saturday");

        Assert.Equal("Sara Khan", key);
    }
}

public class ThreadRegistryPhase1Tests
{
    [Fact]
    public void UpsertFromTriageItem_PreservesFirstInboundOnFollowUpMessage()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var firstInbound = DateTimeOffset.UtcNow.AddMinutes(-40);
        var followUp = DateTimeOffset.UtcNow.AddMinutes(-5);

        registry.UpsertFromTriageItem(
            CreateItem("inst", "Sara", firstInbound, "msg-1"),
            "923001234567@c.us",
            "F-11");

        registry.UpsertFromTriageItem(
            CreateItem("inst", "Sara", followUp, "msg-2"),
            "923001234567@c.us",
            "F-11");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.InRange((DateTimeOffset.UtcNow - thread.FirstInboundAtUtc).TotalMinutes, 39.5, 40.5);
        Assert.False(thread.IsReplied);
    }

    [Fact]
    public void UpsertFromTriageItem_DoesNotReopenResolvedThreadOnLlmEnrichment()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var inboundAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var resolvedAt = DateTimeOffset.UtcNow.AddMinutes(-2);

        registry.UpsertFromTriageItem(
            CreateItem("inst", "Noor", inboundAt, "triage-1"),
            "Noor",
            "F-11");
        registry.MarkThreadResolved("inst", "Noor", "Noor", resolvedAt, "whatsappbusiness");

        registry.UpsertFromTriageItem(
            CreateItem("inst", "Noor", inboundAt, "triage-1"),
            "Noor",
            "F-11",
            nextActionSummary: "LLM enriched summary");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsReplied);
        Assert.Equal("LLM enriched summary", thread.NextActionSummary);
    }

    [Fact]
    public void MarkThreadResolved_UsesProvidedPlatformForStubThread()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.MarkThreadResolved(
            "inst-x",
            "review:rev-1",
            "Reviewer",
            DateTimeOffset.UtcNow,
            "googlebusiness");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.Equal("googlebusiness", thread.Platform);
        Assert.Equal("review:rev-1", thread.ConversationKey);
    }

    [Fact]
    public void ResolveAndMark_GoogleReviewKeyMatchesTriageAndResolve()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var reviewKey = ConversationKeyResolver.BuildReviewKey("rev-99");

        registry.UpsertFromTriageItem(
            CreateItem("inst", "Reviewer", DateTimeOffset.UtcNow, "triage-review", reviewKey),
            reviewKey,
            "F-11");

        registry.MarkThreadResolved("inst", reviewKey, "Reviewer", DateTimeOffset.UtcNow, "googlebusiness");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsReplied);
        Assert.Equal(reviewKey, thread.ConversationKey);
    }

    [Fact]
    public void UpsertFromTriageItem_PreservesHeuristicRevenueLeakageRisk()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var item = CreateItem(
            "inst",
            "Sara",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "triage-leak",
            isRevenueLeakageRisk: true);

        registry.UpsertFromTriageItem(item, "Sara", "F-11");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsRevenueLeakageRisk);
    }

    [Fact]
    public void RefreshOperationalFlags_PreservesExistingRevenueLeakageRisk()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var item = CreateItem(
            "inst",
            "Sara",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "triage-leak-2",
            isRevenueLeakageRisk: true);

        registry.UpsertFromTriageItem(item, "Sara", "F-11");
        registry.RefreshOperationalFlags();

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsRevenueLeakageRisk);
    }

    private static UnifiedMessenger.Models.MessageTriageItem CreateItem(
        string instanceId,
        string customer,
        DateTimeOffset timestampUtc,
        string id,
        string? conversationKey = null,
        bool isRevenueLeakageRisk = false) =>
        new()
        {
            Id = id,
            InstanceId = instanceId,
            InstanceDisplayName = "Depilex F-11",
            Platform = "whatsappbusiness",
            MessagePreview = "Need appointment",
            CustomerName = customer,
            ConversationKey = conversationKey ?? customer,
            UrgencyScore = 30,
            Sentiment = UnifiedMessenger.Models.MessageSentiment.Neutral,
            TimestampUtc = timestampUtc,
            IsRevenueLeakageRisk = isRevenueLeakageRisk,
            AiIntentCategory = UnifiedMessenger.Models.UnifiedMessengerIntentCategory.Inquiry
        };
}
