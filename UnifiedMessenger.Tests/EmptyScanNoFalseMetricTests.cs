using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-SNAP-02 — pins the invariant that stops a broken scraper from being reported as a healthy account.
///
/// A WhatsApp Web schema change makes the scan complete but parse zero conversations. The rollup's
/// on-time calculation has a <c>snapActive &gt; 0 ? … : 100</c> fallback, so such an account computes
/// <c>OnTimePercent == 100</c>. That number is only safe because <c>MeasuredCount</c> is 0 and every
/// consumer gates on it — the card renders "no activity" and the caught-up tile renders "—".
///
/// If MeasuredCount ever became non-zero for an empty snapshot, a broken scraper would render as
/// "100% caught up" — a wrong number presented as fact, silently, on the product's main screen. These
/// tests exist to make that regression impossible to introduce quietly.
/// </summary>
public class EmptyScanNoFalseMetricTests
{
    private static MessengerInstance Inst(string id) =>
        new() { Id = id, DisplayName = id, ProfileName = id, Platform = "whatsapp" };

    /// <summary>
    /// One open thread for an account. Entities are built from threads — an account with no threads
    /// produces no entity at all — so the realistic broken-scraper case is "the account is known and has
    /// threads, but the chat scan parsed nothing", which is what these tests set up.
    /// </summary>
    private static ThreadData Thread(string instanceId) =>
        new()
        {
            ThreadId = Guid.NewGuid().ToString("N"),
            Platform = "whatsapp",
            InstanceId = instanceId,
            InstanceDisplayName = instanceId,
            BranchName = "branch",
            IsReplied = false,
            UrgencyScore = 1,
            LatencyMinutes = 5,
            LastMessageTime = DateTimeOffset.UtcNow
        };

    private static readonly List<MessengerInstance> OneAccount = [Inst("acct-a")];

    /// <summary>A scan that completed but parsed nothing: a snapshot exists, with zero active chats.</summary>
    private static Func<string, (int Active, int CaughtUp)?> EmptySnapshot => _ => (0, 0);

    /// <summary>A healthy account, for contrast.</summary>
    private static Func<string, (int Active, int CaughtUp)?> HealthySnapshot => _ => (10, 4);

    [Fact]
    public void EmptyScanReportsZeroMeasured_WhichIsWhatSuppressesTheFalseHundredPercent()
    {
        var snap = OversightRollupBuilder.Build(
            [Thread("acct-a")], OneAccount, OversightGrouping.ByInstance, _ => 15,
            chatSnapshot: EmptySnapshot);

        var entity = snap.Entities.Single();

        // THE load-bearing assertion. Every UI consumer computes hasLiveData = MeasuredCount > 0.
        Assert.Equal(0, entity.MeasuredCount);

        // And it must not invent a backlog either.
        Assert.Equal(0, entity.AwaitingCount);
    }

    [Fact]
    public void EmptyScanNeverReportsANegativeBacklog()
    {
        // Guards the awaiting = active - caughtUp subtraction against an inverted snapshot.
        var snap = OversightRollupBuilder.Build(
            [Thread("acct-a")], OneAccount, OversightGrouping.ByInstance, _ => 15,
            chatSnapshot: _ => (2, 5));

        Assert.True(snap.Entities.Single().AwaitingCount >= 0);
    }

    [Fact]
    public void HealthyAccountStillMeasuresNormally()
    {
        // Proves the tests above are pinning the empty case specifically, not a blanket "always zero".
        var snap = OversightRollupBuilder.Build(
            [Thread("acct-a")], OneAccount, OversightGrouping.ByInstance, _ => 15,
            chatSnapshot: HealthySnapshot);

        var entity = snap.Entities.Single();

        Assert.Equal(10, entity.MeasuredCount);
        Assert.Equal(6, entity.AwaitingCount);      // 10 active - 4 caught up
        Assert.Equal(40, entity.OnTimePercent);     // 4/10
    }

    [Fact]
    public void AWeightedAverageAcrossAccountsIgnoresTheEmptyOne()
    {
        // Mirrors CommandCenterPanel's KPI aggregation: entities with MeasuredCount == 0 are filtered out
        // before the weighted average, so a broken account cannot drag the headline toward a false 100%.
        var accounts = new List<MessengerInstance> { Inst("good"), Inst("broken") };

        var snap = OversightRollupBuilder.Build(
            [Thread("good"), Thread("broken")], accounts, OversightGrouping.ByInstance, _ => 15,
            chatSnapshot: id => id == "good" ? (10, 4) : (0, 0));

        var live = snap.Entities.Where(e => e.MeasuredCount > 0).ToList();
        var measured = live.Sum(e => e.MeasuredCount);
        var overall = (int)Math.Round(live.Sum(e => (long)e.OnTimePercent * e.MeasuredCount) / (double)measured);

        Assert.Single(live);          // the broken account is excluded entirely
        Assert.Equal(40, overall);    // not 70, which is what averaging in a false 100% would give
    }

    [Fact]
    public void WhenEveryAccountIsBrokenThereIsNothingToAverage()
    {
        // The headline must render "—", never "100%". Reproduces the guard: measured == 0 => no percentage.
        var accounts = new List<MessengerInstance> { Inst("a"), Inst("b") };

        var snap = OversightRollupBuilder.Build(
            [Thread("a"), Thread("b")], accounts, OversightGrouping.ByInstance, _ => 15,
            chatSnapshot: EmptySnapshot);

        var measured = snap.Entities.Where(e => e.MeasuredCount > 0).Sum(e => e.MeasuredCount);

        Assert.Equal(0, measured);
        Assert.All(snap.Entities, e => Assert.Equal(0, e.AwaitingCount));
    }
}
