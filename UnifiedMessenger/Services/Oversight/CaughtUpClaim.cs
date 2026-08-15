using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Whether the product may tell the owner they are all caught up — and, when it may not, why.
///
/// <para>
/// <b>The defect this closes.</b> The hero and the shift briefing both decided this with
/// <c>totalAwaiting == 0</c> alone. An account whose read failed contributes <b>zero</b> awaiting, because
/// there is no data to count — so one branch dropping out of the rollup made the number go <i>down</i>,
/// and with the others genuinely quiet the dashboard showed a green tick and "You're all caught up" while
/// a branch was not being measured at all. The per-account card underneath said "couldn't read"; the
/// headline above it did not look. That is the same shape as the rounding lie and the hero misattribution
/// — a reassuring figure that is reassuring precisely because data is missing.
/// </para>
/// <para>
/// <b>Two kinds of unmeasured, told apart on purpose.</b> An account that failed to read is a problem; an
/// account that has not loaded yet is normal, and v4.99.19 was specifically about not calling the second
/// one broken. Neither may be counted as caught up, but they are worded differently.
/// </para>
/// <para>
/// <b>Staleness deliberately does NOT block the claim.</b> A stale account has real data that is merely
/// old, and sessions go stale routinely as the LRU reaper does its job. Blocking on staleness would mean
/// the claim almost never appears, which trades a rare overclaim for a permanently useless headline. The
/// card already surfaces staleness on its own.
/// </para>
/// </summary>
public static class CaughtUpClaim
{
    /// <summary>The verdict, plus what stopped it when there is no clean claim to make.</summary>
    public readonly record struct Verdict(
        bool CanClaim,
        int AwaitingCount,
        int UnreadableCount,
        int NotLoadedCount,
        int CarriedBacklogCount)
    {
        /// <summary>True when nothing is waiting but some account could not be counted.</summary>
        public bool NothingWaitingButIncomplete =>
            !CanClaim && AwaitingCount == 0 && (UnreadableCount > 0 || NotLoadedCount > 0);

        /// <summary>
        /// True when the claim is honest *for the selected window* but unanswered conversations older
        /// than it are still open. The claim is then scoped rather than absolute — "caught up on today"
        /// is true; "no customers are waiting" is not.
        /// </summary>
        public bool CaughtUpButCarryingBacklog => CanClaim && CarriedBacklogCount > 0;
    }

    /// <summary>
    /// Resolve whether "all caught up" is honest. <paramref name="totalAwaiting"/> is passed in rather
    /// than recomputed so this cannot drift from the number the caller is about to render beside it.
    /// </summary>
    public static Verdict Resolve(IReadOnlyList<OversightEntityHealth>? entities, int totalAwaiting)
    {
        if (entities is null || entities.Count == 0)
        {
            // Nothing to speak for. The caller already hides the hero when there is no measured data.
            return new Verdict(false, totalAwaiting, 0, 0, 0);
        }

        var unreadable = 0;
        var notLoaded = 0;
        var carried = 0;
        foreach (var entity in entities)
        {
            if (entity is null)
            {
                continue;
            }

            if (entity.ReadFailed)
            {
                unreadable++;
            }
            else if (!entity.HasChatData)
            {
                notLoaded++;
            }

            // Unanswered conversations older than the selected window. By design these are kept out of
            // the window's awaiting count rather than saturating it — but they are still customers
            // waiting, so they qualify the claim even though they do not block it.
            carried += Math.Max(0, entity.HistoricalOpenCount);
        }

        return new Verdict(
            totalAwaiting == 0 && unreadable == 0 && notLoaded == 0,
            totalAwaiting,
            unreadable,
            notLoaded,
            carried);
    }

    /// <summary>The hero headline for a verdict where nothing is waiting.</summary>
    public static string Headline(Verdict verdict) =>
        verdict switch
        {
            // Scoped, not absolute: true of the window the owner selected, and it says so rather than
            // implying the whole backlog is clear.
            { CaughtUpButCarryingBacklog: true } => "Caught up on this range",
            { CanClaim: true } => "You're all caught up",
            _ => "Nothing waiting — but not everything was counted"
        };

    /// <summary>
    /// The clause naming unanswered conversations that predate the selected window, or empty when there
    /// are none.
    /// </summary>
    public static string CarriedBacklogClause(Verdict verdict) =>
        verdict.CarriedBacklogCount <= 0
            ? string.Empty
            : $"{verdict.CarriedBacklogCount} older {(verdict.CarriedBacklogCount == 1 ? "conversation is" : "conversations are")} still open from before this range";

    /// <summary>
    /// The clause naming what was not counted, or empty when everything was. Kept separate so callers can
    /// splice it into their own sentence.
    /// </summary>
    public static string IncompleteClause(Verdict verdict)
    {
        var parts = new List<string>(2);
        if (verdict.UnreadableCount > 0)
        {
            parts.Add($"{verdict.UnreadableCount} {Account(verdict.UnreadableCount)} could not be read");
        }

        if (verdict.NotLoadedCount > 0)
        {
            parts.Add($"{verdict.NotLoadedCount} {Account(verdict.NotLoadedCount)} not loaded yet");
        }

        return parts.Count == 0 ? string.Empty : string.Join(" and ", parts);
    }

    private static string Account(int count) => count == 1 ? "account" : "accounts";
}
