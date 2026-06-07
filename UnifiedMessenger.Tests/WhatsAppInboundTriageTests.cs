using System.Reflection;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.Backfill;

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

    [Fact]
    public void Enqueue_DedupesBackfillAndLiveWithinWindow()
    {
        BackfillDedupeRegistry.ClearForTests();
        var instance = new MessengerInstance
        {
            Id = "wa-dedupe",
            DisplayName = "WhatsApp",
            Platform = "whatsapp",
            Category = WorkspaceCategory.Professional
        };

        const string message = "Duplicate preview body for dedupe validation.";

        Assert.True(BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, "Alex", message));
        Assert.False(BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, "Alex", message));

        var triage = new MessageTriageService();
        var selection = new InboundMessageSelection
        {
            InstanceId = instance.Id,
            Platform = instance.Platform,
            MessageText = message,
            CustomerName = "Alex",
            ConversationHint = "Alex",
            TimestampUtc = DateTimeOffset.UtcNow
        };

        triage.Enqueue(selection, instance.DisplayName, skipDedupeCheck: true);
        triage.ProcessInboundForTests(selection, instance.DisplayName);

        Assert.Single(triage.GetAllItems());

        triage.Enqueue(selection, instance.DisplayName);
        Assert.Single(triage.GetAllItems());
    }
}
