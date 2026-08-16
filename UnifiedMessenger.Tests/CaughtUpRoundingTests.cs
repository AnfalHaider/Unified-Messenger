using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-02 — "100% caught up" must mean nobody is waiting.
///
/// The caught-up percentage is computed with <c>Math.Round</c>, so 996 of 1000 chats handled is 99.6%,
/// which rounds to <b>100</b>. The same card also renders "4 awaiting" from the exact counts. Two figures
/// on screen then contradict each other, and the one a busy owner reads first — a green 100% with a tick
/// glyph — is the one that is wrong.
///
/// The rule these tests pin: 100% is reserved for genuinely zero outstanding. Anything short of that
/// rounds DOWN, so the headline can under-claim but never over-claim.
/// </summary>
public class CaughtUpRoundingTests
{
    private static MessengerInstance Inst(string id) =>
        new() { Id = id, DisplayName = id, ProfileName = id, Platform = "whatsapp" };

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

    private static OversightEntityHealth Build(int active, int caughtUp) =>
        OversightRollupBuilder.Build(
            [Thread("acct")],
            [Inst("acct")],
            OversightGrouping.ByInstance,
            _ => 15,
            chatSnapshot: _ => (active, caughtUp)).Entities.Single();

    [Fact]
    public void NinetyNinePointSixPercentDoesNotDisplayAsOneHundred()
    {
        // THE defect. 996/1000 = 99.6% -> Math.Round -> 100, alongside "4 awaiting" on the same card.
        var entity = Build(active: 1000, caughtUp: 996);

        Assert.Equal(4, entity.AwaitingCount);
        Assert.True(
            entity.OnTimePercent < 100,
            $"4 customers are waiting but the card claims {entity.OnTimePercent}% caught up");
    }

    [Fact]
    public void ASingleWaitingCustomerAmongManyDoesNotDisplayAsOneHundred()
    {
        // The most likely real case: a well-run branch with one straggler.
        var entity = Build(active: 500, caughtUp: 499);

        Assert.Equal(1, entity.AwaitingCount);
        Assert.True(
            entity.OnTimePercent < 100,
            $"1 customer is waiting but the card claims {entity.OnTimePercent}% caught up");
    }

    [Fact]
    public void GenuinelyCaughtUpStillReportsOneHundred()
    {
        // The guard must not cost the honest case its 100%.
        var entity = Build(active: 250, caughtUp: 250);

        Assert.Equal(0, entity.AwaitingCount);
        Assert.Equal(100, entity.OnTimePercent);
    }

    [Fact]
    public void AnAccountWithNoActiveChatsIsNotReportedAsBehind()
    {
        // Nothing to answer is not a failure — this is the documented 100% fallback, and it is masked
        // from the UI anyway because MeasuredCount is 0 (see EmptyScanNoFalseMetricTests).
        var entity = Build(active: 0, caughtUp: 0);

        Assert.Equal(0, entity.AwaitingCount);
        Assert.Equal(0, entity.MeasuredCount);
    }

    [Theory]
    [InlineData(1000, 996)]   // 99.6
    [InlineData(500, 499)]    // 99.8
    [InlineData(200, 199)]    // 99.5  — the exact midpoint
    [InlineData(10000, 9999)] // 99.99
    public void AnyOutstandingChatKeepsThePercentageBelowOneHundred(int active, int caughtUp)
    {
        var entity = Build(active, caughtUp);

        Assert.True(entity.AwaitingCount > 0);
        Assert.True(
            entity.OnTimePercent < 100,
            $"{entity.AwaitingCount} waiting but reported {entity.OnTimePercent}%");
    }

    [Fact]
    public void RoundingDownNeverProducesZeroForAnAccountDoingSomeWork()
    {
        // The opposite over-correction: 1 of 1000 handled is 0.1%, which floors to 0. Showing a flat 0%
        // for an account that has handled something is its own small lie, so it must report at least 1.
        var entity = Build(active: 1000, caughtUp: 1);

        Assert.True(entity.OnTimePercent >= 1, $"reported {entity.OnTimePercent}% despite 1 chat handled");
        Assert.True(entity.OnTimePercent < 100);
    }

    [Fact]
    public void ZeroHandledIsStillZero()
    {
        var entity = Build(active: 100, caughtUp: 0);

        Assert.Equal(0, entity.OnTimePercent);
        Assert.Equal(100, entity.AwaitingCount);
    }
}
