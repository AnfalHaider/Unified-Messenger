using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-PARSE-01 / F-PARSE-02 — ChatEntryParser against scraped JSON that has changed shape.
///
/// This parser consumes output from a web page that can change without notice. Both JS producers already
/// wrap each chat in its own try/catch so a malformed chat is skipped rather than failing the scan; the
/// C# side did not, so one unexpected value discarded every conversation already parsed and silently
/// zeroed the account's metrics. These tests pin the two rules that follow: a bad row never costs a good
/// row, and a wrong-typed field is a skipped row rather than an exception.
/// </summary>
public class ChatEntryParserResilienceTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private const string GoodRow = """
        {
          "conversationKey": "923001234567@c.us",
          "customerName": "Ayesha",
          "unreadCount": 2,
          "lastActivityTimestampUtc": "2026-08-10T09:00:00Z",
          "lastMessagePreview": "is the salon open today?",
          "awaiting": true,
          "lastMessageFromMe": false,
          "contactPhone": "923001234567"
        }
        """;

    [Fact]
    public void ParsesAWellFormedRow()
    {
        var list = ChatEntryParser.ParseConversations(Json($$"""{ "conversations": [{{GoodRow}}] }"""));

        var entry = Assert.Single(list);
        Assert.Equal("923001234567@c.us", entry.ConversationKey);
        Assert.Equal("Ayesha", entry.CustomerName);
        Assert.Equal(2, entry.Unread);
        Assert.True(entry.IsAwaiting);
        Assert.False(entry.LastMessageFromMe);
        Assert.Equal("923001234567", entry.ContactPhone);
    }

    [Fact]
    public void ANumericTimestampSkipsOnlyThatRow_AndDoesNotDiscardTheScan()
    {
        // THE regression. GetString() throws InvalidOperationException on a number, which used to escape
        // the whole loop — so one chat emitting an epoch instead of an ISO string zeroed the account.
        var raw = $$"""
            {
              "conversations": [
                { "conversationKey": "a", "lastActivityTimestampUtc": 1754812800000 },
                {{GoodRow}}
              ]
            }
            """;

        var list = ChatEntryParser.ParseConversations(Json(raw));

        Assert.Single(list);
        Assert.Equal("Ayesha", list[0].CustomerName);
    }

    [Fact]
    public void WrongTypedStringFieldsDegradeToEmpty_RatherThanThrowing()
    {
        // Any of these being a number or boolean used to throw out of the entire parse.
        var raw = """
            {
              "conversations": [{
                "conversationKey": 12345,
                "customerName": true,
                "lastMessagePreview": { "nested": "object" },
                "contactPhone": 923001234567,
                "unreadCount": 1,
                "lastActivityTimestampUtc": "2026-08-10T09:00:00Z"
              }]
            }
            """;

        var list = ChatEntryParser.ParseConversations(Json(raw));

        var entry = Assert.Single(list);
        Assert.Equal(string.Empty, entry.ConversationKey);
        Assert.Equal(string.Empty, entry.CustomerName);
        Assert.Equal(string.Empty, entry.Preview);
        Assert.Equal(string.Empty, entry.ContactPhone);
    }

    [Fact]
    public void OneBadRowAmongManyGoodOnesCostsOnlyItself()
    {
        var raw = $$"""
            {
              "conversations": [
                {{GoodRow}},
                { "lastActivityTimestampUtc": "not-a-date" },
                {{GoodRow}},
                "a bare string where an object belongs",
                {{GoodRow}}
              ]
            }
            """;

        var list = ChatEntryParser.ParseConversations(Json(raw));

        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void RowsWithNoTimestampAreDropped_NotStampedWithNow()
    {
        // A fabricated "now" would place a stale chat inside the current activity window and inflate
        // today's counts.
        var raw = """{ "conversations": [{ "conversationKey": "a", "unreadCount": 3 }] }""";

        Assert.Empty(ChatEntryParser.ParseConversations(Json(raw)));
    }

    [Fact]
    public void MissingAwaitingFallsBackToUnread_WhichIsTheDocumentedDegradedBehaviour()
    {
        // Both producers emit `awaiting` today. If that stops, the fallback keeps a number on screen but
        // switches to a per-device, less accurate definition — so the behaviour is pinned deliberately
        // here, and the parser logs when it happens.
        var withUnread = """{ "conversations": [{ "unreadCount": 4, "lastActivityTimestampUtc": "2026-08-10T09:00:00Z" }] }""";
        var withoutUnread = """{ "conversations": [{ "unreadCount": 0, "lastActivityTimestampUtc": "2026-08-10T09:00:00Z" }] }""";

        Assert.True(ChatEntryParser.ParseConversations(Json(withUnread))[0].IsAwaiting);
        Assert.False(ChatEntryParser.ParseConversations(Json(withoutUnread))[0].IsAwaiting);
    }

    [Fact]
    public void ExplicitAwaitingFalseBeatsANonZeroUnreadCount()
    {
        // Direction-based awaiting is authoritative: we replied, but the chat is still unread on a device.
        // Inferring from unread here would resurrect a chat that needs no reply.
        var raw = """
            {
              "conversations": [{
                "unreadCount": 7,
                "awaiting": false,
                "lastMessageFromMe": true,
                "lastActivityTimestampUtc": "2026-08-10T09:00:00Z"
              }]
            }
            """;

        var entry = Assert.Single(ChatEntryParser.ParseConversations(Json(raw)));
        Assert.False(entry.IsAwaiting);
        Assert.True(entry.LastMessageFromMe);
    }

    [Theory]
    [InlineData("""{ "conversations": [] }""")]
    [InlineData("""{ "conversations": null }""")]
    [InlineData("""{ "conversations": "not-an-array" }""")]
    [InlineData("""{ "somethingElse": 1 }""")]
    [InlineData("""{}""")]
    [InlineData("""[]""")]
    [InlineData("""null""")]
    public void MalformedRootsYieldAnEmptyListRatherThanThrowing(string raw)
    {
        Assert.Empty(ChatEntryParser.ParseConversations(Json(raw)));
    }

    [Fact]
    public void UnreadCountThatIsNotAnIntegerDefaultsToZero()
    {
        var raw = """
            {
              "conversations": [{
                "unreadCount": "lots",
                "awaiting": true,
                "lastActivityTimestampUtc": "2026-08-10T09:00:00Z"
              }]
            }
            """;

        var entry = Assert.Single(ChatEntryParser.ParseConversations(Json(raw)));
        Assert.Equal(0, entry.Unread);
        Assert.True(entry.IsAwaiting);
    }

    [Fact]
    public void TimestampsAreNormalisedToUtc()
    {
        // Local-offset timestamps must not shift a chat into the wrong activity day.
        var raw = """
            {
              "conversations": [{
                "awaiting": true,
                "lastActivityTimestampUtc": "2026-08-10T14:00:00+05:00"
              }]
            }
            """;

        var entry = Assert.Single(ChatEntryParser.ParseConversations(Json(raw)));
        Assert.Equal(TimeSpan.Zero, entry.LastActivityUtc.Offset);
        Assert.Equal(9, entry.LastActivityUtc.Hour);
    }
}
