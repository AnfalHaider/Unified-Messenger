using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The Instagram reader (A13). Parsing is separated from transport so it can be exercised without a
/// WebView; the JS assertions below guard the two field-level traps that would each ship a wrong number.
/// </summary>
public class InstagramReaderTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string ScriptText()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "instagram-adapter.js");
        return File.ReadAllText(path);
    }

    [Fact]
    public void ParsesNameHandleTimestampAndAwaiting()
    {
        var chats = InstagramSnapshotReader.ParseConversations(Parse("""
        {
          "conversations": [
            { "key": "t1", "name": "Raja Anas", "username": "raja.anas", "unread": 1,
              "awaiting": true, "lastActivityMs": 1757000000000 }
          ]
        }
        """));

        var chat = Assert.Single(chats);
        Assert.Equal("t1", chat.ConversationKey);
        Assert.Equal("Raja Anas", chat.CustomerName);
        Assert.Equal(1, chat.Unread);
        Assert.True(chat.IsAwaiting);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1757000000000), chat.LastActivityUtc);
    }

    [Fact]
    public void PreviewIsAlwaysEmptyAndThatIsNotAFailedRead()
    {
        var chats = InstagramSnapshotReader.ParseConversations(Parse("""
        { "conversations": [ { "key": "t1", "name": "A", "unread": 1, "awaiting": true, "lastActivityMs": 1 } ] }
        """));

        // The feed's Relay prefetch carries thread metadata only — a sweep for any snippet-shaped field
        // returns empty. The surface must say the text stays in Instagram rather than render a blank
        // preview, which reads as a read that failed.
        Assert.Equal(string.Empty, Assert.Single(chats).Preview);

        // Null means "this snapshot did not record it", which is NOT the same as false. False would be a
        // positive claim that the chat has no last message — the deleted-for-everyone signal.
        Assert.Null(chats[0].HasLastMessage);
    }

    [Fact]
    public void AThreadWithNoTitleFallsBackToTheHandleThenTheKey()
    {
        var chats = InstagramSnapshotReader.ParseConversations(Parse("""
        {
          "conversations": [
            { "key": "t1", "name": "", "username": "someone", "unread": 0, "awaiting": false, "lastActivityMs": 1 },
            { "key": "t2", "name": "", "username": "", "unread": 0, "awaiting": false, "lastActivityMs": 1 }
          ]
        }
        """));

        // An untitled thread is rare but real (a brand-new request). An empty name renders as a blank row
        // the owner cannot act on, which is worse than an ugly one.
        Assert.Equal("@someone", chats[0].CustomerName);
        Assert.Equal("t2", chats[1].CustomerName);
    }

    [Fact]
    public void AConversationWithNoKeyIsSkippedRatherThanGivenABlankOne()
    {
        var chats = InstagramSnapshotReader.ParseConversations(Parse("""
        { "conversations": [ { "key": "", "name": "Ghost", "unread": 1, "awaiting": true, "lastActivityMs": 1 } ] }
        """));

        // The key is the identity used for mark-handled and snooze. A blank one would collide with every
        // other blank one, so an override applied to one chat would silently apply to all of them.
        Assert.Empty(chats);
    }

    [Fact]
    public void MissingOrMalformedPayloadYieldsNoChatsRatherThanThrowing()
    {
        Assert.Empty(InstagramSnapshotReader.ParseConversations(Parse("""{ "diag": { "stage": "empty" } }""")));
        Assert.Empty(InstagramSnapshotReader.ParseConversations(Parse("""{ "conversations": "not-an-array" }""")));
        Assert.Empty(InstagramSnapshotReader.ParseConversations(Parse("{}")));
    }

    [Theory]
    // A direct contradiction with the client's own uncapped count.
    [InlineData(15, 2, false, true)]
    // Equal is fine, and so is fewer: those are ordinary settled reads.
    [InlineData(2, 2, false, false)]
    [InlineData(0, 2, false, false)]
    [InlineData(0, 0, false, false)]
    // Fewer than the badge is EXPECTED on a busy account: the badge counts every unread thread while the
    // reader sees the top 15 of Primary.
    [InlineData(15, 20, false, false)]
    // A missing badge means Instagram omitted the prefix, which it does when nothing is unread.
    [InlineData(3, null, false, true)]
    [InlineData(0, null, false, false)]
    // THE CAPPED FORM, and the reason this parameter exists. Instagram writes "(9+) Instagram" past nine.
    // v4.99.89 shipped a digits-only pattern that could not parse it, read the badge as zero, and
    // discarded all 15 genuinely-unread threads on the busiest account in the workspace. A capped badge is
    // a lower bound, so it can never contradict anything and must never reject.
    [InlineData(15, 9, true, false)]
    [InlineData(200, 9, true, false)]
    public void AnUnreadCountAboveTheClientsOwnBadgeIsRejected(
        int awaiting, int? badge, bool capped, bool expected) =>
        Assert.Equal(expected, InstagramSnapshotReader.LooksLikeAnUnsyncedRead(awaiting, badge, capped));

    [Fact]
    public void TheScriptParsesTheCappedBadgeForm()
    {
        var script = ScriptText();

        // The regex must tolerate "(9+)". A digits-only group returns null there, the reader treats that
        // as a badge of zero, and the guard then throws away every thread on a busy account.
        Assert.Contains(@"(\d+)(\+?)", script, StringComparison.Ordinal);
        Assert.Contains("unreadBadgeCapped", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScriptReportsTheClientsOwnBadgeSoTheReadCanBeCrossChecked()
    {
        var script = ScriptText();

        // The badge is an independent readback of the same fact the resolver reports. Without it the
        // reader has nothing to check itself against, and the unsynced window ships thirteen invented
        // waiting customers into the queue.
        Assert.Contains("unreadBadge", script, StringComparison.Ordinal);
        Assert.Contains("document.title", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScriptReadsTheResolverAndNotTheManualUnreadFlag()
    {
        var script = ScriptText();

        // marked_as_unread is the manual "Mark as unread" flag, NOT the unread state. Measured live: it
        // read false on all 15 threads of an account whose own badge said 6, so a reader trusting it
        // reports every account permanently caught up.
        Assert.Contains("$r:client__is_unread", script, StringComparison.Ordinal);
        Assert.DoesNotContain("record.marked_as_unread", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScriptNeverNavigatesOpensAThreadOrPagesTheConnection()
    {
        var script = ScriptText();

        // The whole safety case for this channel: it reads records the client already fetched for its own
        // badge. Navigating, opening a thread, or following the connection's end_cursor would each turn a
        // passive read into an action a real customer can see.
        Assert.DoesNotContain("location.href =", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/direct/t/", script, StringComparison.Ordinal);
        Assert.DoesNotContain("end_cursor", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".click(", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScriptCutsTextWithoutSplittingASurrogatePair()
    {
        var script = ScriptText();

        // A raw slice through an emoji leaves a lone surrogate, and System.Text.Json then throws on that
        // property — which once dropped a real conversation from every single scan.
        Assert.Contains("0xd800", script, StringComparison.Ordinal);
        Assert.Contains("safeTruncate", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstagramIsMeasuredButKeepsTheThreadOpenProhibition()
    {
        var caps = PlatformDefinition.CapabilitiesFor("instagram");

        Assert.True(caps.ContributesConversationMetrics);
        Assert.True(caps.CanReadUnread);
        Assert.True(caps.CanReadTimestamps);
        Assert.True(caps.CanReadContactIdentity);

        // Never claimed: the feed carries no preview text, and no reply timing is obtainable — so
        // Instagram is excluded from the on-time denominator rather than scored as a miss.
        Assert.False(caps.CanReadPreview);
        Assert.False(caps.SupportsFrt);

        // Still banned from opening a thread. Reading a message body there fires a read receipt at a real
        // customer, and listing who is waiting does not require it — the two facts do not have to agree.
        Assert.True(caps.RequiresThreadOpenToRead);

        // And it is NOT aggregate-only: it lists every waiting customer by name.
        Assert.False(caps.IsAggregateOnly);
        Assert.Equal(ChannelCoverageLevel.NoMessageText, ChannelCoverage.For("instagram"));
    }
}
