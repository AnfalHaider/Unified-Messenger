using System.Text.Json;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class ChatEntryParserTests
{
    private static JsonElement Root(string conversationsJson) =>
        JsonDocument.Parse($"{{\"conversations\":{conversationsJson}}}").RootElement;

    [Fact]
    public void ParseConversations_ReadsEveryField_IncludingLastMessageFromMe()
    {
        // lastMessageFromMe is the field the old backfill loop silently dropped — assert it round-trips, since
        // ApplyStickyAwaiting depends on it.
        var root = Root("""
        [{
            "conversationKey": "923001234567@c.us",
            "customerName": "Ayesha",
            "unreadCount": 2,
            "lastActivityTimestampUtc": "2026-07-10T08:30:00Z",
            "lastMessagePreview": "are you open today?",
            "awaiting": true,
            "lastMessageFromMe": false,
            "contactPhone": "923001234567"
        }]
        """);

        var entry = Assert.Single(ChatEntryParser.ParseConversations(root));
        Assert.Equal("923001234567@c.us", entry.ConversationKey);
        Assert.Equal("Ayesha", entry.CustomerName);
        Assert.Equal(2, entry.Unread);
        Assert.Equal("are you open today?", entry.Preview);
        Assert.True(entry.IsAwaiting);
        Assert.False(entry.LastMessageFromMe);
        Assert.Equal("923001234567", entry.ContactPhone);
        Assert.Equal(new DateTimeOffset(2026, 7, 10, 8, 30, 0, TimeSpan.Zero), entry.LastActivityUtc);
    }

    [Fact]
    public void ParseConversations_FromMeTrue_RoundTrips()
    {
        var root = Root("""
        [{ "conversationKey": "x@c.us", "lastActivityTimestampUtc": "2026-07-10T08:30:00Z",
           "awaiting": false, "lastMessageFromMe": true }]
        """);

        var entry = Assert.Single(ChatEntryParser.ParseConversations(root));
        Assert.True(entry.LastMessageFromMe);
        Assert.False(entry.IsAwaiting);
    }

    [Fact]
    public void ParseConversations_AwaitingAbsent_FallsBackToUnread()
    {
        var root = Root("""
        [{ "conversationKey": "a@c.us", "unreadCount": 3, "lastActivityTimestampUtc": "2026-07-10T08:30:00Z" },
         { "conversationKey": "b@c.us", "unreadCount": 0, "lastActivityTimestampUtc": "2026-07-10T08:30:00Z" }]
        """);

        var entries = ChatEntryParser.ParseConversations(root);
        Assert.True(entries[0].IsAwaiting);   // unread > 0
        Assert.False(entries[1].IsAwaiting);  // unread == 0
    }

    [Fact]
    public void ParseConversations_SkipsRowWithNoParseableTimestamp()
    {
        var root = Root("""
        [{ "conversationKey": "good@c.us", "lastActivityTimestampUtc": "2026-07-10T08:30:00Z" },
         { "conversationKey": "no-ts@c.us" },
         { "conversationKey": "bad-ts@c.us", "lastActivityTimestampUtc": "not-a-date" }]
        """);

        var entry = Assert.Single(ChatEntryParser.ParseConversations(root));
        Assert.Equal("good@c.us", entry.ConversationKey);
    }

    [Fact]
    public void ParseConversations_NoConversationsArray_ReturnsEmpty()
    {
        Assert.Empty(ChatEntryParser.ParseConversations(JsonDocument.Parse("{}").RootElement));
    }
}
