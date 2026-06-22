using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public sealed class OversightThreadEnricherTests
{
    private static OversightChatSnapshotService.ChatEntry MakeChat(
        string conversationKey,
        string customerName = "New message",
        string preview = "") =>
        new(conversationKey, customerName, Unread: 1, LastActivityUtc: DateTimeOffset.UtcNow,
            Preview: preview, IsAwaiting: true, LastMessageFromMe: false);

    [Fact]
    public void Enrich_ReturnsContactPhoneNumber_WhenContextHasIt()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var ctx = WhatsAppBusinessContextService.CreateForTests();

        ctx.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = "inst1",
            ConversationKey = "abc123@lid",
            CustomerName = "New message",
            ContactPhoneNumber = "923085431101"
        });

        var chat = MakeChat("abc123@lid");
        var (name, _) = OversightThreadEnricher.Enrich("inst1", chat, registry, ctx);

        Assert.Equal("+923085431101", name);
    }

    [Fact]
    public void Enrich_UsesContextCustomerName_WhenPhoneAbsentButNameReal()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var ctx = WhatsAppBusinessContextService.CreateForTests();

        ctx.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = "inst1",
            ConversationKey = "923001234567@c.us",
            CustomerName = "Ahmed Khan"
        });

        var chat = MakeChat("923001234567@c.us");
        var (name, _) = OversightThreadEnricher.Enrich("inst1", chat, registry, ctx);

        Assert.Equal("Ahmed Khan", name);
    }

    [Fact]
    public void Enrich_UsesThreadRegistryPreview_WhenAvailable()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var ctx = WhatsAppBusinessContextService.CreateForTests();

        // Simulate the notification ingress having recorded a preview.
        registry.UpsertFromTriageItem(new MessageTriageItem
        {
            Id = Guid.NewGuid().ToString(),
            InstanceId = "inst1",
            InstanceDisplayName = "DHA-1",
            Platform = "whatsapp",
            CustomerName = "Ahmed Khan",
            ConversationKey = "923001234567@c.us",
            MessagePreview = "Is my appointment confirmed?",
            TimestampUtc = DateTimeOffset.UtcNow,
            MessageKind = InboundMessageKind.Text
        }, conversationKey: "923001234567@c.us", branchName: "DHA-1");

        var chat = MakeChat("923001234567@c.us", preview: "");
        var (_, preview) = OversightThreadEnricher.Enrich("inst1", chat, registry, ctx);

        Assert.Equal("Is my appointment confirmed?", preview);
    }

    [Fact]
    public void Enrich_MatchesByPhoneLocalPart_ForCusJid()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var ctx = WhatsAppBusinessContextService.CreateForTests();

        // Notification ingress stored thread under bare phone digits (no @c.us suffix).
        registry.UpsertFromTriageItem(new MessageTriageItem
        {
            Id = Guid.NewGuid().ToString(),
            InstanceId = "inst1",
            InstanceDisplayName = "DHA-1",
            Platform = "whatsapp",
            CustomerName = "Customer",
            ConversationKey = "923001234567",
            MessagePreview = "Hi, what are your hours?",
            TimestampUtc = DateTimeOffset.UtcNow,
            MessageKind = InboundMessageKind.Text
        }, conversationKey: "923001234567", branchName: "DHA-1");

        // IndexedDB scan returns the full JID.
        var chat = MakeChat("923001234567@c.us", preview: "");
        var (_, preview) = OversightThreadEnricher.Enrich("inst1", chat, registry, ctx);

        Assert.Equal("Hi, what are your hours?", preview);
    }

    [Fact]
    public void Enrich_FallsBackToChatValues_WhenNoMatch()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var ctx = WhatsAppBusinessContextService.CreateForTests();

        var chat = MakeChat("abc123@lid", customerName: "New message", preview: "");
        var (name, preview) = OversightThreadEnricher.Enrich("inst1", chat, registry, ctx);

        Assert.Equal("New message", name);
        Assert.Equal("", preview);
    }

    [Fact]
    public void Enrich_IgnoresGenericNames_InContextAndRegistry()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var ctx = WhatsAppBusinessContextService.CreateForTests();

        ctx.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = "inst1",
            ConversationKey = "abc@lid",
            CustomerName = "Customer"
        });

        var chat = MakeChat("abc@lid", customerName: "My real name");
        var (name, _) = OversightThreadEnricher.Enrich("inst1", chat, registry, ctx);

        // Context has a generic name — should fall through to the chat's own CustomerName.
        Assert.Equal("My real name", name);
    }
}
