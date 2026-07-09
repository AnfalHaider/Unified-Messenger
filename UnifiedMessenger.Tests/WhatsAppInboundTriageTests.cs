using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class WhatsAppInboundTriageTests
{
    [Fact]
    public void WhatsAppAdapter_ResolvesForWhatsAppPlatform()
    {
        var adapter = PlatformAdapterFactory.Resolve("whatsapp");
        Assert.Equal("whatsapp", adapter.PlatformId);
    }

    [Fact]
    public async Task InboundMessageSelected_EnqueuesTriageForWhatsApp()
    {
        var triage = new MessageTriageService();
        triage.ProcessInboundForTests(new InboundMessageSelection
        {
            InstanceId = "wa-1",
            Platform = "whatsapp",
            MessageText = "I need an appointment tomorrow please",
            CustomerName = "Noor",
            ConversationKey = "noor@c.us"
        });

        await Task.Delay(100);

        var items = triage.GetAllItems();
        Assert.NotEmpty(items);
        Assert.Contains(items, item => item.CustomerName == "Noor");
    }
}
