using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ThreadRegistryResolveAliasTests
{
    private const string InstanceId = "inst-1";
    private const string Jid = "120363045678901@c.us";
    private const string DisplayName = "Zohra";

    [Fact]
    public void MarkThreadResolved_DisplayNameAliasMatchesJidThread()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.UpsertFromTriageItem(
            CreateItem(DisplayName),
            Jid,
            "DHA-2");

        registry.MarkThreadResolved(InstanceId, DisplayName, DisplayName, DateTimeOffset.UtcNow, "whatsapp");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsReplied);
        Assert.Equal(Jid, thread.ConversationKey);
        Assert.Equal(DisplayName, thread.CustomerName);
    }

    [Fact]
    public void MarkThreadResolved_JidAliasMatchesDisplayNameThread()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.UpsertFromTriageItem(
            CreateItem(DisplayName),
            DisplayName,
            "DHA-2");

        registry.MarkThreadResolved(InstanceId, Jid, DisplayName, DateTimeOffset.UtcNow, "whatsapp");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsReplied);
        Assert.Equal(DisplayName, thread.ConversationKey);
    }

    [Fact]
    public void TryGetUnrepliedThread_MatchesJidAgainstDisplayNameThread()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.UpsertFromTriageItem(
            CreateItem(DisplayName),
            Jid,
            "DHA-2");

        var found = registry.TryGetUnrepliedThread(
            InstanceId,
            DisplayName,
            DisplayName,
            "whatsapp",
            out var thread);

        Assert.True(found);
        Assert.NotNull(thread);
        Assert.Equal(Jid, thread!.ConversationKey);
    }

    [Fact]
    public void MarkThreadResolved_WithoutExistingThread_DoesNotCreateThread()
    {
        var registry = ThreadRegistryService.CreateForTests();

        registry.MarkThreadResolved(InstanceId, DisplayName, DisplayName, DateTimeOffset.UtcNow, "whatsapp");

        Assert.Empty(registry.GetAllThreads());
    }

    [Fact]
    public void ReconcileConversationKey_MigratesTitleKeyedThreadToJid()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.UpsertFromTriageItem(CreateItem(DisplayName), DisplayName, "DHA-2");

        var migrated = registry.ReconcileConversationKey(InstanceId, Jid, DisplayName);

        Assert.True(migrated);
        var thread = Assert.Single(registry.GetAllThreads());
        Assert.Equal(Jid, thread.ConversationKey);
        Assert.Equal(DisplayName, thread.CustomerName);
    }

    [Fact]
    public void ReconcileConversationKey_NoOp_WhenStableKeyedThreadAlreadyExists()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.UpsertFromTriageItem(CreateItem(DisplayName), Jid, "DHA-2");

        var migrated = registry.ReconcileConversationKey(InstanceId, Jid, DisplayName);

        Assert.False(migrated);
        Assert.Single(registry.GetAllThreads());
    }

    private static MessageTriageItem CreateItem(string customer) =>
        new()
        {
            Id = $"{InstanceId}|seed",
            InstanceId = InstanceId,
            InstanceDisplayName = "Depilex DHA-2",
            Platform = "whatsapp",
            MessagePreview = "Need bridal quote",
            CustomerName = customer,
            UrgencyScore = 40,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow
        };
}
