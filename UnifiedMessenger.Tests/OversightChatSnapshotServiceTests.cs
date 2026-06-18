using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class OversightChatSnapshotServiceTests
{
    [Fact]
    public void TryGetWindowed_ScopesActiveByLastActivity()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = "inst-" + Guid.NewGuid().ToString("N");

        svc.Update(id, new[]
        {
            new OversightChatSnapshotService.ChatEntry("jid-a", "A", 0, now),              // today, caught up
            new OversightChatSnapshotService.ChatEntry("jid-b", "B", 2, now),              // today, awaiting
            new OversightChatSnapshotService.ChatEntry("jid-c", "C", 0, now.AddDays(-3)),  // older, caught up
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
            new OversightChatSnapshotService.ChatEntry("a", "A", 1, now.AddDays(-10)), // before range
            new OversightChatSnapshotService.ChatEntry("b", "B", 1, now.AddDays(-5)),  // inside range
            new OversightChatSnapshotService.ChatEntry("c", "C", 0, now),              // after range
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
    public void TryGetWindowed_NoSnapshot_ReturnsFalse()
    {
        Assert.False(OversightChatSnapshotService.Instance
            .TryGetWindowed("missing-" + Guid.NewGuid().ToString("N"), null, out _, out _));
    }
}
