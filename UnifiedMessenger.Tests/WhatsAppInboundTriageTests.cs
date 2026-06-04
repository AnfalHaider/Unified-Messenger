using System.Reflection;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

public class WhatsAppInboundTriageTests
{
    [Fact]
    public void WhatsAppAdapter_EnablesInboundMonitoringScripts()
    {
        var adapter = PlatformAdapterFactory.Resolve("whatsapp");

        var supportsInbound = adapter
            .GetType()
            .GetProperty(
                "SupportsInboundAutoDraft",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetValue(adapter);

        Assert.True(supportsInbound as bool?);
    }

    [Fact]
    public async Task InboundMessageSelected_EnqueuesTriageForWhatsApp()
    {
        var triage = new MessageTriageService();
        var instance = new MessengerInstance
        {
            Id = "wa-pro",
            DisplayName = "WhatsApp Sales",
            Platform = "whatsapp",
            Category = WorkspaceCategory.Professional
        };

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = instance.Id,
                Platform = instance.Platform,
                MessageText = "Hi, I need to reschedule my appointment for Saturday.",
                CustomerName = "Sara",
                ConversationHint = "Sara",
                TimestampUtc = DateTimeOffset.UtcNow
            },
            instance.DisplayName);

        await Task.Delay(50);

        var snapshot = triage.BuildSnapshot([instance]);
        Assert.NotEmpty(snapshot.UrgentQueue);
        Assert.True(snapshot.PositiveCount + snapshot.NeutralCount + snapshot.NegativeCount > 0);
    }
}
