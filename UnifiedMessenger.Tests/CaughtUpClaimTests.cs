using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-STATE-01 — the dashboard could show a green tick and "You're all caught up" while a branch was not
/// being measured at all.
///
/// <para>
/// This is the all-caught-up cell of the state matrix, which the handoff recorded as never having been
/// reached: the owner's live data has never hit zero awaiting, so the branch had never run. Reaching it
/// synthetically is what exposed the defect.
/// </para>
/// <para>
/// <b>Why it is the dangerous direction.</b> An account whose read fails contributes <b>zero</b> awaiting,
/// because there is nothing to count. So a branch dropping out of the rollup pushes the total <i>down</i>,
/// towards the reassuring answer. Both the hero and the shift briefing decided "caught up" from
/// <c>totalAwaiting == 0</c> alone and never looked at <c>ReadFailed</c> — which the per-account card
/// directly underneath was already rendering as "couldn't read".
/// </para>
/// </summary>
public class CaughtUpClaimTests
{
    private static OversightEntityHealth Entity(
        string key,
        int awaiting = 0,
        bool readFailed = false,
        bool hasChatData = true,
        bool stale = false,
        int historical = 0) =>
        new()
        {
            Key = key,
            DisplayName = key,
            Kind = OversightEntityKind.Instance,
            AccountCount = 1,
            AwaitingCount = awaiting,
            MeasuredCount = hasChatData ? 10 : 0,
            HasChatData = hasChatData,
            ReadFailed = readFailed,
            IsStale = stale,
            HistoricalOpenCount = historical,
            OnTimePercent = 100,
            MemberInstanceIds = [key]
        };

    // ---- The honest cases ---------------------------------------------------------------------------

    [Fact]
    public void EverythingReadAndNothingWaitingIsAGenuineAllCaughtUp()
    {
        var verdict = CaughtUpClaim.Resolve([Entity("a"), Entity("b")], totalAwaiting: 0);

        Assert.True(verdict.CanClaim);
        Assert.Equal("You're all caught up", CaughtUpClaim.Headline(verdict));
        Assert.Empty(CaughtUpClaim.IncompleteClause(verdict));
    }

    [Fact]
    public void AnythingWaitingIsNotCaughtUp()
    {
        var verdict = CaughtUpClaim.Resolve([Entity("a", awaiting: 1), Entity("b")], totalAwaiting: 1);

        Assert.False(verdict.CanClaim);
        Assert.False(verdict.NothingWaitingButIncomplete);
    }

    // ---- The defect ---------------------------------------------------------------------------------

    [Fact]
    public void AnUnreadableAccountBlocksTheClaimEvenWhenNothingElseIsWaiting()
    {
        // The exact live shape: two quiet branches, one whose WhatsApp could not be read. Before the fix
        // this rendered a green tick and "You're all caught up".
        var verdict = CaughtUpClaim.Resolve(
            [Entity("quiet-1"), Entity("quiet-2"), Entity("broken", readFailed: true)],
            totalAwaiting: 0);

        Assert.False(verdict.CanClaim);
        Assert.True(verdict.NothingWaitingButIncomplete);
        Assert.Equal(1, verdict.UnreadableCount);
        Assert.DoesNotContain("all caught up", CaughtUpClaim.Headline(verdict), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be read", CaughtUpClaim.IncompleteClause(verdict), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnAccountThatHasNotLoadedYetAlsoBlocksTheClaimButIsWordedDifferently()
    {
        // v4.99.19 was specifically about not calling a not-yet-loaded account broken. It still cannot be
        // counted as caught up — but it must not be described as a failure either.
        var verdict = CaughtUpClaim.Resolve(
            [Entity("quiet"), Entity("asleep", hasChatData: false)],
            totalAwaiting: 0);

        Assert.False(verdict.CanClaim);
        Assert.Equal(0, verdict.UnreadableCount);
        Assert.Equal(1, verdict.NotLoadedCount);

        var clause = CaughtUpClaim.IncompleteClause(verdict);
        Assert.Contains("not loaded yet", clause, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("could not be read", clause, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BothKindsOfUnmeasuredAreNamedTogether()
    {
        var verdict = CaughtUpClaim.Resolve(
            [Entity("a", readFailed: true), Entity("b", readFailed: true), Entity("c", hasChatData: false)],
            totalAwaiting: 0);

        var clause = CaughtUpClaim.IncompleteClause(verdict);

        Assert.Contains("2 accounts could not be read", clause, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 account not loaded yet", clause, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheClauseIsSingularForOneAccountAndPluralForMore()
    {
        Assert.Contains(
            "1 account could not be read",
            CaughtUpClaim.IncompleteClause(CaughtUpClaim.Resolve([Entity("a", readFailed: true)], 0)),
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "3 accounts could not be read",
            CaughtUpClaim.IncompleteClause(CaughtUpClaim.Resolve(
                [Entity("a", readFailed: true), Entity("b", readFailed: true), Entity("c", readFailed: true)], 0)),
            StringComparison.OrdinalIgnoreCase);
    }

    // ---- Deliberate non-blockers --------------------------------------------------------------------

    [Fact]
    public void StalenessDoesNotBlockTheClaim()
    {
        // A stale account has real data that is merely old, and sessions go stale routinely as the LRU
        // reaper works. Blocking on staleness would make the claim almost never appear — trading a rare
        // overclaim for a permanently useless headline. The card surfaces staleness on its own.
        var verdict = CaughtUpClaim.Resolve([Entity("a", stale: true), Entity("b")], totalAwaiting: 0);

        Assert.True(verdict.CanClaim);
    }

    [Fact]
    public void AnUnreadableAccountThatAlsoHasNoChatDataIsCountedOnceAsUnreadable()
    {
        // A failed read implies no chat data. Counting it in both buckets would report two problems where
        // there is one.
        var verdict = CaughtUpClaim.Resolve(
            [Entity("broken", readFailed: true, hasChatData: false)], totalAwaiting: 0);

        Assert.Equal(1, verdict.UnreadableCount);
        Assert.Equal(0, verdict.NotLoadedCount);
    }

    // ---- The date-range cell: backlog older than the selected window --------------------------------

    [Fact]
    public void BacklogOlderThanTheWindowScopesTheClaimInsteadOfBlockingIt()
    {
        // The date-range interaction. With "Today" selected, a conversation last active a week ago is
        // deliberately kept OUT of the window's awaiting count so it does not saturate today's number —
        // that is the documented design. But the customer is still waiting, so an unqualified
        // "You're all caught up" / "No customers are waiting on a reply" was false in the way that costs
        // a customer.
        var verdict = CaughtUpClaim.Resolve([Entity("a", historical: 300), Entity("b")], totalAwaiting: 0);

        Assert.True(verdict.CanClaim);              // honest about the selected range
        Assert.True(verdict.CaughtUpButCarryingBacklog);
        Assert.Equal(300, verdict.CarriedBacklogCount);

        Assert.Equal("Caught up on this range", CaughtUpClaim.Headline(verdict));
        Assert.Contains("300 older conversations are still open", CaughtUpClaim.CarriedBacklogClause(verdict), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoOlderBacklogGivesTheUnqualifiedClaim()
    {
        var verdict = CaughtUpClaim.Resolve([Entity("a"), Entity("b")], totalAwaiting: 0);

        Assert.False(verdict.CaughtUpButCarryingBacklog);
        Assert.Equal("You're all caught up", CaughtUpClaim.Headline(verdict));
        Assert.Empty(CaughtUpClaim.CarriedBacklogClause(verdict));
    }

    [Fact]
    public void OneOlderConversationReadsAsSingular()
    {
        var verdict = CaughtUpClaim.Resolve([Entity("a", historical: 1)], totalAwaiting: 0);

        Assert.Contains("1 older conversation is still open", CaughtUpClaim.CarriedBacklogClause(verdict), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BacklogIsSummedAcrossAccounts()
    {
        var verdict = CaughtUpClaim.Resolve(
            [Entity("a", historical: 12), Entity("b", historical: 30), Entity("c")], totalAwaiting: 0);

        Assert.Equal(42, verdict.CarriedBacklogCount);
    }

    [Fact]
    public void AnUnreadableAccountStillOutranksCarriedBacklog()
    {
        // Both qualifiers present: not-counted is the more serious one and must win the headline, because
        // the backlog figure itself is incomplete when an account could not be read.
        var verdict = CaughtUpClaim.Resolve(
            [Entity("a", historical: 5), Entity("b", readFailed: true)], totalAwaiting: 0);

        Assert.False(verdict.CanClaim);
        Assert.False(verdict.CaughtUpButCarryingBacklog);
        Assert.Contains("not everything was counted", CaughtUpClaim.Headline(verdict), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnboundedWindowHasNoCarriedBacklogByConstruction()
    {
        // HistoricalOpenCount is 0 whenever windowStartUtc is null, so "All time" can never produce the
        // scoped wording. Pinned so the two stay consistent.
        var verdict = CaughtUpClaim.Resolve([Entity("a", historical: 0), Entity("b", historical: 0)], totalAwaiting: 0);

        Assert.Equal(0, verdict.CarriedBacklogCount);
        Assert.Equal("You're all caught up", CaughtUpClaim.Headline(verdict));
    }

    // ---- Degenerate input ---------------------------------------------------------------------------

    [Fact]
    public void NoEntitiesNeverProducesAClaim()
    {
        // Nothing to speak for is not the same as nothing to do. The caller hides the hero entirely in
        // this state; the resolver must not hand it a cheerful answer if that ever changes.
        Assert.False(CaughtUpClaim.Resolve([], 0).CanClaim);
        Assert.False(CaughtUpClaim.Resolve(null, 0).CanClaim);
    }
}
