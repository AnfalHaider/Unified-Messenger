using UnifiedMessenger.Services;
using Kpi = UnifiedMessenger.ViewModels.KpiTileViewModel;

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

    // ---- What the tile actually says -------------------------------------------------------------------

    [Fact]
    public void TheHintLeadsWithWhatTheAppCouldNotRead()
    {
        // An unreadable chat is the one case where the number itself is uncertain, so it is said first.
        // Without it, a scrape that failed to read message bodies looks exactly like a quiet morning.
        var split = new OversightChatSnapshotService.AwaitingSplit(
            NeedsReply: 79, Backlog: 283, ClosedAutomatically: 104, Unreadable: 35);

        var hint = UnifiedMessenger.Controls.CommandCenterPanel.BuildAwaitingHint(split, accountsBehind: 3);

        Assert.StartsWith("35 unreadable", hint, StringComparison.Ordinal);
        Assert.Contains("283 older", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void AClearQueueFallsBackToTheAccountSummary()
    {
        var split = new OversightChatSnapshotService.AwaitingSplit(0, 0, 0, 0);

        Assert.Equal(
            "all accounts clear",
            UnifiedMessenger.Controls.CommandCenterPanel.BuildAwaitingHint(split, accountsBehind: 0));
    }

    [Fact]
    public void TheTooltipAccountsForEveryConversationTheHeadlineLeftOut()
    {
        // The headline is now a subset of what it used to be. An owner who remembers a bigger number is
        // owed an explanation of where the rest went, and a route to check it.
        var split = new OversightChatSnapshotService.AwaitingSplit(79, 283, 104, 35);

        var tooltip = UnifiedMessenger.Controls.CommandCenterPanel.BuildAwaitingTooltip(split);

        Assert.Contains("283", tooltip, StringComparison.Ordinal);
        Assert.Contains("104", tooltip, StringComparison.Ordinal);
        Assert.Contains("35", tooltip, StringComparison.Ordinal);
        Assert.Contains("Settings", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsMentionedThatIsNotThere()
    {
        // A tooltip that always recites every category would tell a brand-new user about a backlog and an
        // exclusion list they do not have.
        var tooltip = UnifiedMessenger.Controls.CommandCenterPanel.BuildAwaitingTooltip(
            new OversightChatSnapshotService.AwaitingSplit(4, 0, 0, 0));

        Assert.DoesNotContain("more have been waiting", tooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("were not counted", tooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be read", tooltip, StringComparison.Ordinal);
    }

    // ---- The cold-scan trap ---------------------------------------------------------------------------

    [Fact]
    public void AColdScanCannotCloseTheWholeQueue()
    {
        // This shipped and was caught on the live app: chat.msgs fills in lazily, so a scan taken seconds
        // after a reload reports "no last message" for almost every chat. Read literally, that says every
        // customer's message was deleted — and 354 real conversations rendered as 5.
        //
        // The scrapers now retract the claim when coverage is low, and the snapshot loader repeats the
        // retraction so a file written by a cold scan cannot keep closing the queue on every launch. This
        // test pins the loader half, because that is the half that survives a restart.
        var path = Path.Combine(Path.GetTempPath(), $"um-cold-{Guid.NewGuid():N}.json");
        try
        {
            // Ten conversations, nine of them reporting no message, all old enough to be "gone".
            var chats = Enumerable.Range(0, 10).Select(i => new
            {
                conversationKey = $"9200000000{i}@c.us",
                customerName = $"Customer {i}",
                unread = 0,
                lastActivityUtc = DateTimeOffset.UtcNow.AddDays(-30),
                preview = "",
                isAwaiting = true,
                lastMessageFromMe = false,
                contactPhone = "",
                hasLastMessage = i == 0,
                lastMessageType = "chat"
            }).ToArray();

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                version = 1,
                instances = new Dictionary<string, object>
                {
                    ["acct"] = new { capturedAtUtc = DateTimeOffset.UtcNow, chats }
                }
            });
            File.WriteAllText(path, payload);

            var svc = new OversightChatSnapshotService(path);
            svc.LoadAsync().GetAwaiter().GetResult();

            // Every one must survive: coverage was 1 in 10, so "no message" is not credible for any of them.
            Assert.Equal(10, svc.GetAwaiting("acct", null).Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AWarmScanIsStillTrustedToCloseTheOnesThatAreGone()
    {
        // The retraction must not make the signal useless. With coverage high, a single conversation
        // reporting no message is believable and does get closed.
        var path = Path.Combine(Path.GetTempPath(), $"um-warm-{Guid.NewGuid():N}.json");
        try
        {
            var chats = Enumerable.Range(0, 10).Select(i => new
            {
                conversationKey = $"9230000000{i}@c.us",
                customerName = $"Customer {i}",
                unread = 0,
                lastActivityUtc = DateTimeOffset.UtcNow.AddDays(-30),
                preview = i == 0 ? "" : "kitna charge hoga",
                isAwaiting = true,
                lastMessageFromMe = false,
                contactPhone = "",
                hasLastMessage = i != 0,
                lastMessageType = "chat"
            }).ToArray();

            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
            {
                version = 1,
                instances = new Dictionary<string, object>
                {
                    ["acct2"] = new { capturedAtUtc = DateTimeOffset.UtcNow, chats }
                }
            }));

            var svc = new OversightChatSnapshotService(path);
            svc.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(9, svc.GetAwaiting("acct2", null).Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---- KPI tile semantics ---------------------------------------------------------------------------
    //
    // These assert the STATIC composition helpers rather than the view model itself. Constructing
    // KpiTileViewModel requires a Brush, and Brush needs the XAML runtime, which a headless test host does
    // not have — the first version of these tests failed with a bare COMException for exactly that reason.

    [Fact]
    public void EveryKpiTileTellsAScreenReaderWhatItMeasuresAndWhetherItDoesAnything()
    {
        // The tiles were a Border with a Tapped handler: not focusable, no Invoke pattern, no name. A UI
        // Automation capture of the running app showed them as unrelated Text nodes, so the drill-down into
        // the reply queue was mouse-only and invisible to assistive tech.
        var name = Kpi.ComposeAccessibleName(
            "Backlog", "184", string.Empty, "61 need a reply now", hasAction: true);

        Assert.Contains("Backlog: 184", name, StringComparison.Ordinal);
        Assert.Contains("61 need a reply now", name, StringComparison.Ordinal);
        Assert.Contains("Press to see details", name, StringComparison.Ordinal);
    }

    [Fact]
    public void ATileWithNoDrillDownDoesNotInviteAPress()
    {
        // Rendering all tiles as Buttons keeps one visual treatment, but a tile that does nothing must not
        // claim to be pressable, and IsTabStop is bound off so it collects no dead tab stop either.
        var name = Kpi.ComposeAccessibleName(
            "Busiest window", "7PM", string.Empty, "peak hour", hasAction: false);

        Assert.DoesNotContain("Press to", name, StringComparison.Ordinal);
        Assert.Contains("Busiest window: 7PM", name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("▼ 59%", "down 59%")]
    [InlineData("▲ 12", "up 12")]
    public void ADeltaGlyphIsReadAsWordsNotAsAnArrowCharacter(string delta, string expected)
    {
        var name = Kpi.ComposeAccessibleName("Response time", "8.6h", delta, string.Empty, false);

        Assert.Contains(expected, name, StringComparison.Ordinal);
        Assert.DoesNotContain("▼", name, StringComparison.Ordinal);
        Assert.DoesNotContain("▲", name, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactlyOneTileIsAllowedToBeLoud()
    {
        // Six tiles at one size is the same as no hierarchy. The primary tile is a full type step above the
        // rest; if that gap ever closes the band stops guiding the eye.
        Assert.True(Kpi.ValueFontSizeFor(true) >= Kpi.ValueFontSizeFor(false) + 4);
    }

    [Fact]
    public void TheBacklogTileDoesNotRepeatTheHerosNumber()
    {
        // The hero renders "needs a reply" at 42px. The tile below it used to render the same figure at
        // 32px, which read as two facts. It now carries what the hero cannot: the backlog.
        var split = new OversightChatSnapshotService.AwaitingSplit(
            NeedsReply: 61, Backlog: 184, ClosedAutomatically: 117, Unreadable: 1);

        var hint = UnifiedMessenger.Controls.CommandCenterPanel.BuildBacklogHint(split, accountsBehind: 3);

        Assert.Contains("61 need a reply now", hint, StringComparison.Ordinal);
        Assert.Contains("1 unreadable", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoBacklogTheTileFallsBackToTheLiveQueue()
    {
        // A business with nothing older than a week should not see an empty "Backlog: 0" tile.
        var split = new OversightChatSnapshotService.AwaitingSplit(4, 0, 0, 0);

        Assert.Equal(
            "all accounts clear",
            UnifiedMessenger.Controls.CommandCenterPanel.BuildBacklogHint(split, accountsBehind: 0));
    }
}
