using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class OversightChatSnapshotServiceTests
{
    // awaiting = the customer had the last word (we haven't replied).
    private static OversightChatSnapshotService.ChatEntry Chat(
        string key, string name, int unread, DateTimeOffset when, bool awaiting, bool fromMe = false) =>
        new(key, name, unread, when, Preview: "", IsAwaiting: awaiting, LastMessageFromMe: fromMe);

    [Fact]
    public void StickyAwaiting_OpeningChatDoesNotMarkResponded()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = "inst-" + Guid.NewGuid().ToString("N");

        // First read: customer is waiting (awaiting, 2 unread).
        svc.Update(id, new[] { Chat("jid-x", "X", 2, now, awaiting: true) }, now);
        Assert.Single(svc.GetAwaiting(id, null));

        // Operator OPENS the chat off-screen → unread clears, direction unconfirmed (fromMe=false).
        // It must STAY awaiting — opening is not replying.
        svc.Update(id, new[] { Chat("jid-x", "X", 0, now, awaiting: false, fromMe: false) }, now);

        var stillAwaiting = Assert.Single(svc.GetAwaiting(id, null));
        Assert.Equal("jid-x", stillAwaiting.ConversationKey);
    }

    [Fact]
    public void StickyAwaiting_ConfirmedReplyClearsAwaiting()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = "inst-" + Guid.NewGuid().ToString("N");

        svc.Update(id, new[] { Chat("jid-y", "Y", 1, now, awaiting: true) }, now);
        Assert.Single(svc.GetAwaiting(id, null));

        // Operator actually REPLIES → last message is now from us (fromMe=true). Awaiting clears.
        svc.Update(id, new[] { Chat("jid-y", "Y", 0, now, awaiting: false, fromMe: true) }, now);

        Assert.Empty(svc.GetAwaiting(id, null));
    }

    [Fact]
    public void StickyAwaiting_NewCaughtUpChatStaysCaughtUp()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = "inst-" + Guid.NewGuid().ToString("N");

        // No prior state for this chat → an awaiting=false read is taken at face value.
        svc.Update(id, new[] { Chat("jid-z", "Z", 0, now, awaiting: false, fromMe: false) }, now);

        Assert.Empty(svc.GetAwaiting(id, null));
    }

    [Fact]
    public void TryGetWindowed_ScopesActiveByLastActivity()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = "inst-" + Guid.NewGuid().ToString("N");

        svc.Update(id, new[]
        {
            Chat("jid-a", "A", 0, now, awaiting: false),              // today, caught up
            Chat("jid-b", "B", 2, now, awaiting: true),               // today, awaiting
            Chat("jid-c", "C", 0, now.AddDays(-3), awaiting: false),  // older, caught up
        }, now);

        var windowStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); // start of today

        Assert.True(svc.TryGetWindowed(id, windowStart, out var active, out var caughtUp));
        Assert.Equal(2, active);    // only the two from today
        Assert.Equal(1, caughtUp);  // one of them caught up

        // No window → all chats counted.
        Assert.True(svc.TryGetWindowed(id, null, out var allActive, out var allCaught));
        Assert.Equal(3, allActive);
        Assert.Equal(2, allCaught);

        // The awaiting list within today is just chat B.
        var awaiting = svc.GetAwaiting(id, windowStart);
        var only = Assert.Single(awaiting);
        Assert.Equal("jid-b", only.ConversationKey);
        Assert.Equal(2, only.Unread);
    }

    [Fact]
    public void TryGetWindowed_RespectsWindowEnd()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = "inst-" + Guid.NewGuid().ToString("N");

        svc.Update(id, new[]
        {
            Chat("a", "A", 1, now.AddDays(-10), awaiting: true), // before range
            Chat("b", "B", 1, now.AddDays(-5), awaiting: true),  // inside range
            Chat("c", "C", 0, now, awaiting: false),             // after range
        }, now);

        var start = now.AddDays(-7);
        var end = now.AddDays(-2);

        Assert.True(svc.TryGetWindowed(id, start, out var active, out _, end));
        Assert.Equal(1, active); // only "b" falls in [start, end]

        var awaiting = svc.GetAwaiting(id, start, end);
        var only = Assert.Single(awaiting);
        Assert.Equal("b", only.ConversationKey);
    }

    [Fact]
    public void BuildDigest_CountsNewTotalAndOldest()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = "inst-" + Guid.NewGuid().ToString("N");

        svc.Update(id, new[]
        {
            Chat("a", "A", 2, now, awaiting: true),               // awaiting, new
            Chat("b", "B", 1, now.AddDays(-2), awaiting: true),   // awaiting, old (oldest)
            Chat("c", "C", 0, now, awaiting: false),              // caught up
        }, now);

        var digest = svc.BuildDigest(new[] { id }, now.AddDays(-1));

        Assert.True(digest.HasData);
        Assert.Equal(2, digest.TotalAwaiting);
        Assert.Equal(1, digest.NewAwaiting);          // only "a" arrived since yesterday
        Assert.Equal(1, digest.AccountsWithAwaiting);
        Assert.Equal(now.AddDays(-2), digest.OldestActivityUtc);
    }

    [Fact]
    public void BuildDigest_NoSnapshot_HasDataFalse()
    {
        var digest = OversightChatSnapshotService.Instance
            .BuildDigest(new[] { "missing-" + Guid.NewGuid().ToString("N") }, null);

        Assert.False(digest.HasData);
        Assert.Equal(0, digest.TotalAwaiting);
    }

    [Fact]
    public void TryGetWindowed_NoSnapshot_ReturnsFalse()
    {
        Assert.False(OversightChatSnapshotService.Instance
            .TryGetWindowed("missing-" + Guid.NewGuid().ToString("N"), null, out _, out _));
    }
}
