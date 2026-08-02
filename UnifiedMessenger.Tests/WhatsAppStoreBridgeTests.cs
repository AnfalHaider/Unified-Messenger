using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Guards for the in-memory store bridge. There is no JS engine in the harness, so the script itself is
/// asserted textually (same approach as <c>WhatsAppBackfillScriptTests</c>); the parts that matter most —
/// that the bridge's output is byte-compatible with the IndexedDB scan's envelope, so
/// <see cref="ChatEntryParser"/> reads either source unchanged — are asserted against real JSON.
/// </summary>
public class WhatsAppStoreBridgeTests
{
    [Fact]
    public void StoreBridge_ExposesHostFacingApi()
    {
        var script = ReadScript();

        Assert.Contains("window.__umStartStoreScan", script, StringComparison.Ordinal);
        Assert.Contains("window.__umGetStoreScanResult", script, StringComparison.Ordinal);
        Assert.Contains("window.__umStoreBridgeProbe", script, StringComparison.Ordinal);
        Assert.Contains("window.__umStore", script, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreBridge_TriesEveryDiscoveryStrategy()
    {
        var script = ReadScript();

        // Module names churn between WhatsApp Web releases, so discovery must have more than one route in.
        Assert.Contains("debug-require", script, StringComparison.Ordinal);
        Assert.Contains("webpack-chunk", script, StringComparison.Ordinal);
        Assert.Contains("module-cache", script, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreBridge_IsReadOnly()
    {
        var script = ReadScript();

        // The app never sends. If a future edit reaches for a mutation surface, this fails loudly.
        foreach (var forbidden in new[]
                 {
                     "sendMessage", "sendText", "markRead", "sendSeen", "deleteMessage",
                     "sendImage", "setPresence", "archiveChat"
                 })
        {
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void StoreBridge_NeverThrowsIntoThePage()
    {
        var script = ReadScript();

        // Every exported entry point wraps its work — an exception escaping into WhatsApp Web could break
        // the user's actual messaging client, which is categorically worse than losing a metric.
        Assert.Contains("catch (error)", script, StringComparison.Ordinal);
        Assert.Contains("scan-exception", script, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreBridge_DropsGroupsAndAnonymousLidChats()
    {
        var script = ReadScript();

        Assert.Contains("@g.us", script, StringComparison.Ordinal);
        Assert.Contains("@broadcast", script, StringComparison.Ordinal);
        Assert.Contains("@newsletter", script, StringComparison.Ordinal);
        // Fully-anonymous @lid privacy contacts can't be identified or opened — same filter the
        // IndexedDB path applies, so the two sources can't disagree on the needs-reply list.
        Assert.Contains("@lid", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeEnvelope_ParsesThroughTheSharedChatEntryParser()
    {
        // This is the contract that lets the host swap sources without touching the parser: the bridge
        // emits exactly the field names the IndexedDB scan emits.
        const string envelope = """
        {
          "ok": true,
          "diag": { "stage": "done", "source": "store-bridge", "strategy": "debug-require", "withPreview": 2 },
          "conversations": [
            {
              "conversationKey": "923105325598@c.us",
              "customerName": "Ayesha",
              "contactPhone": "923105325598",
              "lastInboundBody": "Is the shop open today?",
              "lastInboundTimestampUtc": "2026-08-01T09:15:00.000Z",
              "lastActivityTimestampUtc": "2026-08-01T09:15:00.000Z",
              "lastMessageFromMe": false,
              "awaiting": true,
              "lastMessagePreview": "Is the shop open today?",
              "unreadCount": 2,
              "inboundCount": 2
            },
            {
              "conversationKey": "100012725952736@lid",
              "customerName": "Bilal",
              "contactPhone": "923001234567",
              "lastInboundBody": "",
              "lastInboundTimestampUtc": "2026-08-01T08:00:00.000Z",
              "lastActivityTimestampUtc": "2026-08-01T08:00:00.000Z",
              "lastMessageFromMe": true,
              "awaiting": false,
              "lastMessagePreview": "Sure, see you at 5.",
              "unreadCount": 0,
              "inboundCount": 0
            }
          ]
        }
        """;

        using var doc = JsonDocument.Parse(envelope);
        var entries = ChatEntryParser.ParseConversations(doc.RootElement);

        Assert.Equal(2, entries.Count);

        var awaiting = entries[0];
        Assert.Equal("923105325598@c.us", awaiting.ConversationKey);
        Assert.Equal("Ayesha", awaiting.CustomerName);
        Assert.Equal("923105325598", awaiting.ContactPhone);
        Assert.True(awaiting.IsAwaiting);
        Assert.False(awaiting.LastMessageFromMe);
        Assert.Equal(2, awaiting.Unread);
        // The whole point of the bridge: a real preview, for a chat the DOM harvest may never have reached.
        Assert.Equal("Is the shop open today?", awaiting.Preview);

        var replied = entries[1];
        Assert.Equal("100012725952736@lid", replied.ConversationKey);
        Assert.Equal("923001234567", replied.ContactPhone);
        Assert.False(replied.IsAwaiting);
        Assert.True(replied.LastMessageFromMe);
        Assert.Equal("Sure, see you at 5.", replied.Preview);
    }

    [Fact]
    public void BridgeEnvelope_FailureShapeYieldsNoEntries()
    {
        const string failed = """
        { "ok": false, "conversations": [], "diag": { "stage": "no-store", "source": "store-bridge" } }
        """;

        using var doc = JsonDocument.Parse(failed);
        Assert.Empty(ChatEntryParser.ParseConversations(doc.RootElement));
    }

    private static string ReadScript()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "whatsapp-store-bridge.js");
        Assert.True(File.Exists(scriptPath), $"Missing store bridge script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }
}
