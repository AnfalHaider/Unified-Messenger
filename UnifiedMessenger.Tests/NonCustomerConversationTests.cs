using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-05 and F-METRICS-06 — verified against the owner's real stored snapshot.
///
/// <para>
/// F-METRICS-05: whatsapp-adapter.js excluded WhatsApp's own <c>0@c.us</c> notice account from the scan;
/// whatsapp-store-bridge.js did not. The store bridge is the PREFERRED path, so what users actually saw
/// was the unfiltered version. Found in the live snapshot as
/// <c>{"conversationKey":"0@c.us","customerName":"WhatsApp Business","isAwaiting":true}</c> — a one-way
/// account that cannot be replied to, sitting in the awaiting count for 26 days with no way to clear it.
/// </para>
/// <para>
/// F-METRICS-06: 85 of 3,027 previews in that same snapshot were raw base64 JPEG payloads, rendered where
/// the README promises "the actual text of their last message".
/// </para>
/// <para>
/// Both are enforced in the parser — the one point both producers funnel through — rather than in each
/// scraper, because trusting two producers to stay in step is exactly what failed.
/// </para>
/// </summary>
public class NonCustomerConversationTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static string Row(string key) => $$"""
        {
          "conversationKey": "{{key}}",
          "customerName": "Someone",
          "unreadCount": 1,
          "awaiting": true,
          "lastActivityTimestampUtc": "2026-08-10T09:00:00Z",
          "lastMessagePreview": "hello"
        }
        """;

    [Theory]
    [InlineData("0@c.us")]              // WhatsApp official — the live defect
    [InlineData("0@s.whatsapp.net")]
    [InlineData("923001234567-group@g.us")]
    [InlineData("status@broadcast")]
    [InlineData("1234567890@broadcast")]
    [InlineData("abc@newsletter")]
    public void NonCustomerConversationsAreExcludedFromTheMetrics(string key)
    {
        var list = ChatEntryParser.ParseConversations(Json($$"""{ "conversations": [{{Row(key)}}] }"""));

        Assert.Empty(list);
    }

    [Theory]
    [InlineData("923001234567@c.us")]
    [InlineData("923001234567@s.whatsapp.net")]
    [InlineData("209876543210@lid")]     // unsaved contact privacy JID — a real customer
    [InlineData("10@c.us")]              // must NOT be caught by the 0@ rule
    [InlineData("30@c.us")]
    public void RealCustomerConversationsAreKept(string key)
    {
        var list = ChatEntryParser.ParseConversations(Json($$"""{ "conversations": [{{Row(key)}}] }"""));

        Assert.Single(list);
    }

    [Fact]
    public void FilteringRemovesOnlyTheNonCustomerRows()
    {
        var raw = $$"""
            {
              "conversations": [
                {{Row("923001234567@c.us")}},
                {{Row("0@c.us")}},
                {{Row("team@g.us")}},
                {{Row("923009999999@c.us")}}
              ]
            }
            """;

        var list = ChatEntryParser.ParseConversations(Json(raw));

        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.EndsWith("@c.us", e.ConversationKey, StringComparison.Ordinal));
        Assert.DoesNotContain(list, e => e.ConversationKey.StartsWith("0@", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/9j/4AAQSkZJRgABAQAAAQABAAD/4gHYSUNDX1BST0ZJTEUAAQ")]  // JPEG, the live case
    [InlineData("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAA")]    // PNG
    [InlineData("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAI")] // GIF
    [InlineData("data:image/png;base64,iVBORw0KGgo")]
    public void EncodedMediaPreviewsBecomeAReadableLabel(string preview)
    {
        var raw = $$"""
            {
              "conversations": [{
                "conversationKey": "923001234567@c.us",
                "awaiting": true,
                "lastActivityTimestampUtc": "2026-08-10T09:00:00Z",
                "lastMessagePreview": "{{preview}}"
              }]
            }
            """;

        var entry = Assert.Single(ChatEntryParser.ParseConversations(Json(raw)));

        Assert.Equal("Photo", entry.Preview);
    }

    [Theory]
    [InlineData("is the salon open today?")]
    [InlineData("/9 out of 10 would recommend")]   // starts with '/' but is not base64
    [InlineData("data on my bill looks wrong")]    // starts with 'data' but is not a data URI
    [InlineData("I've attached a photo")]
    public void OrdinaryMessageTextIsNeverRelabelled(string preview)
    {
        var raw = $$"""
            {
              "conversations": [{
                "conversationKey": "923001234567@c.us",
                "awaiting": true,
                "lastActivityTimestampUtc": "2026-08-10T09:00:00Z",
                "lastMessagePreview": "{{preview}}"
              }]
            }
            """;

        var entry = Assert.Single(ChatEntryParser.ParseConversations(Json(raw)));

        Assert.Equal(preview, entry.Preview);
    }
}
