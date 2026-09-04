using UnifiedMessenger.Controls;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for F-METRICS-01 (S1) — the command centre hero line attributing the oldest waiting
/// customer to the wrong account.
///
/// The line is built by joining parts with " · ", which reads as a single sentence. It previously emitted
/// the oldest wait followed by the name of the account with the MOST awaiting chats — two unrelated facts
/// that the join fused into one false claim.
///
/// Observed live before the fix:
///     hero: "oldest 75d · Depilex DHA-2 WhatsApp · 12% caught up overall"
///     that account's own card: "Longest wait: 50d"
/// The 75-day wait belonged to a different account entirely.
/// </summary>
public class HeroSubtextAttributionTests
{
    private const double SeventyFiveDays = 75 * 24 * 60;
    private const double FiftyDays = 50 * 24 * 60;

    [Fact]
    public void OldestWaitIsAttributedToItsOwnAccount_NotToTheAccountFurthestBehind()
    {
        // The exact live scenario that exposed the defect.
        var text = CommandCenterPanel.ComposeHeroSubtext(
            oldestAccountName: "Depilex Men DHA-2 WhatsApp",
            oldestMinutes: SeventyFiveDays,
            worstAccountName: "Depilex DHA-2 WhatsApp");

        // The oldest wait must name the account it belongs to...
        Assert.Contains("oldest 75d (Depilex Men DHA-2 WhatsApp)", text, StringComparison.Ordinal);

        // ...and the account furthest behind must be labelled so it cannot be read as owning that wait.
        Assert.Contains("furthest behind: Depilex DHA-2 WhatsApp", text, StringComparison.Ordinal);

        // The pre-fix output — an unlabelled name straight after the duration — must never reappear.
        Assert.DoesNotContain("oldest 75d · Depilex DHA-2 WhatsApp", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountIsNotNamedTwice_WhenTheOldestWaitIsAtTheAccountFurthestBehind()
    {
        var text = CommandCenterPanel.ComposeHeroSubtext(
            oldestAccountName: "Depilex F-11 WhatsApp",
            oldestMinutes: FiftyDays,
            worstAccountName: "Depilex F-11 WhatsApp");

        // No parenthetical when it would just repeat the name on the same line.
        Assert.Contains("oldest 50d · furthest behind: Depilex F-11 WhatsApp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(Depilex F-11 WhatsApp)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OldestSegmentIsOmitted_WhenThereIsNoWaitToReport()
    {
        var text = CommandCenterPanel.ComposeHeroSubtext(
            oldestAccountName: null,
            oldestMinutes: null,
            worstAccountName: "Some Account");

        Assert.DoesNotContain("oldest", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("furthest behind: Some Account", text);
    }

    [Fact]
    public void SubMinuteWaitsAreNotReportedAsAnOldestWait()
    {
        // Guards the >= 1 minute floor: "oldest 0m" is noise, not information.
        var text = CommandCenterPanel.ComposeHeroSubtext(
            oldestAccountName: "Fresh Account",
            oldestMinutes: 0.4,
            worstAccountName: "Fresh Account");

        Assert.DoesNotContain("oldest", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithNothingToReportTheLineIsEmptyRatherThanPadded()
    {
        // This test used to assert "100% caught up overall" and had been failing in CI ever since the
        // percentage was deliberately removed from the subtext — it is the Caught up tile's job, shown
        // directly below in larger type. Nobody noticed, because the method still ACCEPTED a percentage
        // argument, so the test read as though it was exercising live behaviour.
        var text = CommandCenterPanel.ComposeHeroSubtext(null, null, null);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void UnattributedOldestWaitStillOmitsAMisleadingName()
    {
        // If the owning account cannot be resolved, the line must report the duration alone rather than
        // borrowing the nearest available name — reporting nothing beats reporting the wrong thing.
        var text = CommandCenterPanel.ComposeHeroSubtext(
            oldestAccountName: null,
            oldestMinutes: SeventyFiveDays,
            worstAccountName: "Busy Account");

        Assert.Contains("oldest 75d · furthest behind: Busy Account", text, StringComparison.Ordinal);
        Assert.DoesNotContain("oldest 75d (", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOldestWaitFromTheBacklogSaysSo()
    {
        // Observed live: "35 customers are waiting for a reply · oldest 26d". The hero counts the LIVE
        // queue and excludes the backlog by design, but the oldest wait is searched across everything
        // awaiting — so on any account with a backlog the age comes from a population the number does not
        // include. Joined by " · " it reads as one sentence, and the 26-day customer is taken for one of
        // the 35. Same shape as the 75d-at-the-wrong-account bug above, one level up: an unlabelled
        // duration read as belonging to the figure beside it.
        var text = CommandCenterPanel.ComposeHeroSubtext(
            oldestAccountName: "Busy Account",
            oldestMinutes: SeventyFiveDays,
            worstAccountName: "Busy Account",
            oldestIsBacklog: true);

        Assert.Contains("oldest in the backlog 75d", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOldestWaitInsideTheLiveQueueIsNotLabelledAsBacklog()
    {
        // The qualifier must appear only when it is true, or it becomes noise on every screen and stops
        // being read on the screens where it matters.
        var text = CommandCenterPanel.ComposeHeroSubtext(
            oldestAccountName: "Busy Account",
            oldestMinutes: 90,
            worstAccountName: "Busy Account",
            oldestIsBacklog: false);

        Assert.Contains("oldest 1.5h", text, StringComparison.Ordinal);
        Assert.DoesNotContain("backlog", text, StringComparison.OrdinalIgnoreCase);
    }
}
