using System.Diagnostics;
using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Stage 4 sabotage probe: ingress burst under rapid WebMessage delivery (no live WebView).
/// Documents whether C# ingress coalesces or processes every message synchronously.
/// </summary>
[Collection(UnifiedMessengerSerialCollection.Name)]
public class IngressBurstStressTests
{
    [Fact]
    public void BadgeCountBurst_500Messages_LatestCountWinsWithoutThrow()
    {
        var hub = NotificationHub.CreateForTests();
        var instance = new MessengerInstance
        {
            Id = "burst-wa",
            DisplayName = "Burst Test",
            Platform = "whatsapp",
            ProfileName = "burst-wa"
        };

        var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 500; i++)
        {
            var payload = $$"""
                {
                  "type": "badge-count",
                  "instanceId": "burst-wa",
                  "count": {{i % 50}}
                }
                """;
            adapter.HandleWebMessage(payload, hub, instance);
        }

        sw.Stop();

        Assert.Equal(49, hub.GetBadgeCount("burst-wa"));
        Assert.Equal(49, hub.TotalUnreadCount);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Burst took {sw.ElapsedMilliseconds} ms — possible UI-thread stall risk");
    }

    [Fact]
    public void TelemetryBurst_200Messages_HandlerAcceptsAllWithoutThrow()
    {
        var instance = new MessengerInstance
        {
            Id = $"telemetry-burst-{Guid.NewGuid():N}",
            DisplayName = "Telemetry Burst",
            Platform = "whatsappbusiness"
        };

        var receivedBefore = MessageAnalyticsService.Instance.GetReceivedCount(instance.Id);
        var handled = 0;

        try
        {
            for (var i = 0; i < 200; i++)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    type = "whatsapp-telemetry",
                    instanceId = instance.Id,
                    conversationKey = $"customer-{i % 20}",
                    customerName = "Customer",
                    lastReceivedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    lastReceivedKind = "text"
                });

                using var document = JsonDocument.Parse(payload);
                if (WhatsAppIngressHandler.TryHandle("whatsapp-telemetry", document.RootElement, instance))
                {
                    handled++;
                }
            }

            Assert.Equal(200, handled);
            Assert.Equal(receivedBefore, MessageAnalyticsService.Instance.GetReceivedCount(instance.Id));
        }
        finally
        {
            ThreadRegistryService.Instance.RestoreThreads([]);
        }
    }
}
