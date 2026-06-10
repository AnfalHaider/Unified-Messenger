using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class WhatsAppBusinessPipelineTests : IDisposable
{
    private readonly IReadOnlyList<ThreadData> _originalThreads;

    public WhatsAppBusinessPipelineTests()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
    }

    public void Dispose() => ThreadRegistryService.Instance.RestoreThreads(_originalThreads);

    [Fact]
    public void TryParseResponse_AcceptsWhatsAppMultiIntentSchema()
    {
        const string json = """
            {
              "customerIntent": "BookingRequest",
              "requestedServices": ["Bridal Makeup", "Hair Styling"],
              "branchTarget": "DHA-2",
              "sentiment": "Frustrated",
              "actionableSummary": "Confirm Saturday bridal slot and send deposit link.",
              "suggestedDraftResponse": "Assalam o Alaikum! Saturday slot available at DHA-2 — shall I hold it with 50% advance?"
            }
            """;

        var parsed = MessageTriageInferenceRunner.TryParseResponse(json, out var response);

        Assert.True(parsed);
        Assert.NotNull(response);
        Assert.Equal(UnifiedMessengerIntentCategory.Booking, response!.AiIntentCategory);
        Assert.Equal(ClientSentimentLabel.Frustrated, response.ClientSentiment);
        Assert.Equal("DHA-2", response.BranchTarget);
        Assert.Equal(2, response.RequestedServices.Count);
        Assert.Contains("Assalam", response.SuggestedDraftResponse, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalContextBuilder_IncludesBranchServices()
    {
        var block = WhatsAppOperationalContextBuilder.BuildOperationalReferenceBlock(
            "Depilex DHA-2",
            "DHA-2",
            new WhatsAppConversationMetadata
            {
                BusinessLabels = ["VIP", "Booking Pending"],
                VerifiedBusinessName = "Depilex Salon",
                ContactPhoneNumber = "+923001234567"
            },
            "120363@s.whatsapp.net",
            "wa-1");

        Assert.Contains("Bridal Makeup", block, StringComparison.Ordinal);
        Assert.Contains("VIP", block, StringComparison.Ordinal);
        Assert.Contains("+923001234567", block, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppIngressHandler_StoresThreadContext()
    {
        var instance = new MessengerInstance
        {
            Id = "wa-ctx",
            DisplayName = "Depilex DHA-2",
            Platform = "whatsappbusiness",
            ProfileName = "wa-ctx"
        };

        var json = JsonSerializer.Serialize(new
        {
            type = "whatsapp-thread-context",
            instanceId = instance.Id,
            conversationKey = "120363@s.whatsapp.net",
            customerName = "Sara",
            businessLabels = new[] { "New Customer" },
            verifiedBusinessName = "Depilex",
            contactPhoneNumber = "+923001112233",
            timestampUtc = DateTimeOffset.UtcNow.ToString("O")
        });

        using var doc = JsonDocument.Parse(json);
        Assert.True(WhatsAppIngressHandler.TryHandle("whatsapp-thread-context", doc.RootElement, instance));

        var stored = WhatsAppBusinessContextService.Instance.GetThreadContext(
            instance.Id,
            "120363@s.whatsapp.net");

        Assert.NotNull(stored);
        Assert.Contains("New Customer", stored!.BusinessLabels);
    }

    [Fact]
    public void WhatsAppIngressHandler_UpdatesThreadDeliveryStatus()
    {
        var instance = new MessengerInstance
        {
            Id = "wa-status",
            DisplayName = "Depilex DHA-2",
            Platform = "whatsappbusiness",
            ProfileName = "wa-status"
        };
        var conversationKey = "120363@s.whatsapp.net";
        var threadId = ConversationKeyResolver.BuildThreadId(instance.Id, conversationKey);
        var timestamp = DateTimeOffset.UtcNow;

        ThreadRegistryService.Instance.RestoreThreads([
            new ThreadData
            {
                ThreadId = threadId,
                Platform = instance.Platform,
                InstanceId = instance.Id,
                CustomerName = "Sara",
                ConversationKey = conversationKey,
                BranchName = "DHA-2",
                LastMessageTime = timestamp
            }
        ]);

        var json = JsonSerializer.Serialize(new
        {
            type = "whatsapp-outgoing-status",
            instanceId = instance.Id,
            conversationKey,
            deliveryStatus = "delivered",
            timestampUtc = timestamp.ToString("O")
        });

        using var doc = JsonDocument.Parse(json);
        Assert.True(WhatsAppIngressHandler.TryHandle("whatsapp-outgoing-status", doc.RootElement, instance));

        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .FirstOrDefault(t => t.ThreadId == threadId);
        Assert.NotNull(thread);
        Assert.Equal(WhatsAppDeliveryStatusLabel.Delivered, thread!.WhatsAppDeliveryStatus);
        Assert.Equal(timestamp, thread.WhatsAppDeliveryUpdatedUtc);
    }

    [Fact]
    public void DeliveryStatusPresentation_ShowsWhatsAppLabelsAndColors()
    {
        Assert.True(WhatsAppDeliveryStatusPresentation.ShouldShow(
            "whatsappbusiness",
            WhatsAppDeliveryStatusLabel.Read));
        Assert.Equal("Read ✓✓", WhatsAppDeliveryStatusPresentation.ResolveLabel(WhatsAppDeliveryStatusLabel.Read));
        Assert.Equal("#2563EB", WhatsAppDeliveryStatusPresentation.ResolveBrushHex(WhatsAppDeliveryStatusLabel.Read));
        Assert.Equal("#94A3B8", WhatsAppDeliveryStatusPresentation.ResolveBrushHex(WhatsAppDeliveryStatusLabel.Sent));
    }

    [Fact]
    public void DeliveryStatusPresentation_HidesForNonWhatsApp()
    {
        Assert.False(WhatsAppDeliveryStatusPresentation.ShouldShow(
            "googlebusiness",
            WhatsAppDeliveryStatusLabel.Read));
    }

    [Fact]
    public void WhatsAppAdapterScript_ExposesMetadataHelpers()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "UnifiedMessenger",
            "Assets",
            "Scripts",
            "whatsapp-adapter.js"));

        Assert.Contains("__umWhatsAppExtractChatHeader", script, StringComparison.Ordinal);
        Assert.Contains("whatsapp-thread-context", script, StringComparison.Ordinal);
        Assert.Contains("whatsapp-outgoing-status", script, StringComparison.Ordinal);
        Assert.Contains("detectOutgoingDeliveryStatus", script, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "UnifiedMessenger.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }
}
