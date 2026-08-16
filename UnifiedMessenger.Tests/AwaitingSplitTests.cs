using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The awaiting population, split into the parts an owner can act on separately.
///
/// <para>
/// Measured on a real salon's data, the single "awaiting" number read <b>466, oldest 82 days</b>. Split
/// the same data and it reads <b>79 needing a reply, 283 backlog, 104 closed automatically</b> — and the
/// 79 is a morning's work rather than a wall. These tests pin the arithmetic and, more importantly, the
/// rule that nothing is ever silently dropped: every conversation lands in exactly one bucket, and the
/// buckets add back up to what the raw flag reported.
/// </para>
/// </summary>
public class AwaitingSplitTests
{
    private static OversightChatSnapshotService.ChatEntry Chat(
        string key, string preview, DateTimeOffset when, bool awaiting = true) =>
        new(key, "Customer", 0, when, Preview: preview, IsAwaiting: awaiting);

    private static string NewInstance() => "inst-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void ClosersComeOutOfTheCountAndAgedOnesMoveToBacklog()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = NewInstance();

        svc.Update(id, new[]
        {
            Chat("live-ask", "kitna charge hoga", now.AddHours(-2)),
            Chat("live-ask-2", "can I book for tomorrow?", now.AddDays(-1)),
            Chat("old-ask", "do you do bridal makeup", now.AddDays(-40)),
            Chat("closer", "Ok thanks", now.AddHours(-3)),
            Chat("closer-old", "ji", now.AddDays(-60))
        }, now);

        var split = svc.BuildAwaitingSplit([id], now, backlogAfterDays: 7);

        Assert.Equal(2, split.NeedsReply);
        Assert.Equal(1, split.Backlog);
        Assert.Equal(2, split.ClosedAutomatically);
    }

    [Fact]
    public void EveryAwaitingChatLandsInExactlyOneBucket()
    {
        // The property that makes the split safe to show: subtraction never loses anyone. If a future
        // rule starts dropping chats before they are counted, this is what notices.
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = NewInstance();

        var chats = new[]
        {
            Chat("a", "kya rate hai", now),
            Chat("b", "ok", now),
            Chat("c", "", now.AddDays(-30)),
            Chat("d", "Photo", now.AddDays(-2)),
            Chat("e", "👍", now.AddDays(-90)),
            Chat("f", "Walaikum us salam", now),
            Chat("g", "near chandni chok", now.AddDays(-9)),
            Chat("not-awaiting", "we replied", now, awaiting: false)
        };
        svc.Update(id, chats, now);

        var split = svc.BuildAwaitingSplit([id], now, backlogAfterDays: 7);
        var awaitingTotal = chats.Count(c => c.IsAwaiting);

        Assert.Equal(awaitingTotal, split.NeedsReply + split.Backlog + split.ClosedAutomatically);
        Assert.Equal(split.NeedsReply + split.Backlog, split.TotalOpen);
    }

    [Fact]
    public void AConversationWithNoReadablePreviewIsCountedAndReportedAsUnreadable()
    {
        // 200 of the owner's 466 had no preview text — the scrape had not filled the message body in yet.
        // They must stay counted (the app cannot judge what it cannot read) AND be reported separately,
        // or a failed scrape would look exactly like a quiet day.
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = NewInstance();

        svc.Update(id, new[]
        {
            Chat("blank-1", "", now.AddHours(-1)),
            Chat("blank-2", "   ", now.AddHours(-2)),
            Chat("readable", "kitna time lagega", now.AddHours(-3))
        }, now);

        var split = svc.BuildAwaitingSplit([id], now, backlogAfterDays: 7);

        Assert.Equal(3, split.NeedsReply);
        Assert.Equal(2, split.Unreadable);
        Assert.Equal(0, split.ClosedAutomatically);
    }

    [Fact]
    public void UnreadableCountsOnlyTheLiveQueueNotTheBacklog()
    {
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = NewInstance();

        svc.Update(id, new[]
        {
            Chat("blank-live", "", now.AddHours(-1)),
            Chat("blank-old", "", now.AddDays(-30))
        }, now);

        var split = svc.BuildAwaitingSplit([id], now, backlogAfterDays: 7);

        // "We cannot read 1 of the chats you need to deal with today" is actionable. Folding a month-old
        // unreadable chat into that number would make it alarming and useless.
        Assert.Equal(1, split.Unreadable);
        Assert.Equal(1, split.NeedsReply);
        Assert.Equal(1, split.Backlog);
    }

    [Fact]
    public void AManuallyHandledChatIsNotReportedAsAutomaticallyClosed()
    {
        // Mark-handled is the owner's own decision. Filing it under "the app closed this for you" would
        // misattribute their action and inflate how much the classifier appears to be doing.
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = NewInstance();
        var when = now.AddHours(-1);

        svc.Update(id, new[] { Chat("handled", "kitna charge", when) }, now);
        Assert.Equal(1, svc.BuildAwaitingSplit([id], now).NeedsReply);

        AwaitingOverrideStore.Instance.MarkHandled(id, "handled", when);
        try
        {
            var split = svc.BuildAwaitingSplit([id], now, backlogAfterDays: 7);

            Assert.Equal(0, split.NeedsReply);
            Assert.Equal(0, split.ClosedAutomatically);
        }
        finally
        {
            AwaitingOverrideStore.Instance.Clear(id, "handled");
        }
    }

    [Fact]
    public void TheExcludedListSaysWhichChatsAndWhy()
    {
        // The owner accepted a smaller number on the condition they can check its working. An excluded
        // list with no reasons would just be a different number to take on faith.
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = NewInstance();

        svc.Update(id, new[]
        {
            Chat("ack", "Ok thanks", now),
            Chat("emoji", "👍", now),
            Chat("real", "kab open hota hai", now)
        }, now);

        var closed = svc.GetAutomaticallyClosed([id], now);

        Assert.Equal(2, closed.Count);
        Assert.DoesNotContain(closed, c => c.Chat.ConversationKey == "real");
        Assert.All(closed, c => Assert.False(string.IsNullOrWhiteSpace(c.Verdict.Explain())));
        Assert.Contains(closed, c => c.Verdict.Reason == ReplyNeedReason.Acknowledgement);
        Assert.Contains(closed, c => c.Verdict.Reason == ReplyNeedReason.EmojiOnly);
    }

    [Fact]
    public void TheHeadlineCountAndTheListUnderneathItAlwaysAgree()
    {
        // These come from different methods (TryGetWindowed vs GetAwaiting) that both route through the
        // same predicate. If a future change classifies in one and not the other, the card would show a
        // number the list below it could not account for — which is how the original 466 became
        // untrustworthy in the first place.
        var svc = OversightChatSnapshotService.Instance;
        var now = DateTimeOffset.UtcNow;
        var id = NewInstance();

        svc.Update(id, new[]
        {
            Chat("ask", "price kya hai", now),
            Chat("ack", "ok", now),
            Chat("blank", "", now),
            Chat("emoji", "😍", now)
        }, now);

        Assert.True(svc.TryGetWindowed(id, null, out var active, out var caughtUp));

        var listed = svc.GetAwaiting(id, null);
        Assert.Equal(active - caughtUp, listed.Count);
        Assert.Equal(2, listed.Count); // the question and the unreadable one
    }
}
