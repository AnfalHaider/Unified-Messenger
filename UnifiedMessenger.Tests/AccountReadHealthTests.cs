using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for F-SNAP-02 — "this account is quiet" and "the app can't read this account" used to
/// render identically as "no activity". Both produce zero conversations, so the distinction cannot be
/// derived from the numbers; it has to be carried from a recorded read outcome.
///
/// The dangerous failure mode of the FIX is a false positive: telling an owner their scraper is broken
/// when the branch is simply quiet erodes trust in the opposite direction. Several of these tests exist
/// specifically to pin that the warning stays off unless a failure was actually recorded.
/// </summary>
[Collection("AccountReadHealth")]
public class AccountReadHealthTests : IDisposable
{
    public AccountReadHealthTests() => AccountReadHealth.Reset();

    public void Dispose()
    {
        AccountReadHealth.Reset();
        GC.SuppressFinalize(this);
    }

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

    private static OversightEntityHealth BuildOne(string instanceId) =>
        OversightRollupBuilder.Build(
            [Thread(instanceId)],
            [Inst(instanceId)],
            OversightGrouping.ByInstance,
            _ => 15,
            readFailed: AccountReadHealth.LastReadFailed,
            chatSnapshot: _ => (0, 0)).Entities.Single();

    // ── AccountReadHealth itself ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AnAccountNeverReadIsNotReportedAsFailed()
    {
        // THE false-positive guard. Before the first scan lands, every account is in this state — firing
        // the warning here would show it on every launch and train the owner to ignore it.
        Assert.False(AccountReadHealth.LastReadFailed("never-touched"));
        Assert.Null(AccountReadHealth.TryGet("never-touched"));
    }

    [Fact]
    public void ASuccessfulReadIsNotReportedAsFailed()
    {
        AccountReadHealth.RecordSuccess("acct");

        Assert.False(AccountReadHealth.LastReadFailed("acct"));
    }

    [Fact]
    public void AFailedReadIsReported()
    {
        AccountReadHealth.RecordFailure("acct", "no ingestion path returned usable data");

        Assert.True(AccountReadHealth.LastReadFailed("acct"));
        Assert.Equal("no ingestion path returned usable data", AccountReadHealth.TryGet("acct")!.Value.Reason);
    }

    [Fact]
    public void ARecoveredAccountStopsBeingReportedAsFailed()
    {
        // The warning must clear on its own once a read succeeds, or it becomes permanent noise.
        AccountReadHealth.RecordFailure("acct", "boom");
        Assert.True(AccountReadHealth.LastReadFailed("acct"));

        AccountReadHealth.RecordSuccess("acct");
        Assert.False(AccountReadHealth.LastReadFailed("acct"));
    }

    [Fact]
    public void InstanceIdsAreMatchedCaseInsensitivelyAndTrimmed()
    {
        AccountReadHealth.RecordFailure("Acct-1", "boom");

        Assert.True(AccountReadHealth.LastReadFailed("acct-1"));
        Assert.True(AccountReadHealth.LastReadFailed("  ACCT-1  "));
    }

    [Fact]
    public void BlankInstanceIdsAreIgnoredRatherThanThrowing()
    {
        AccountReadHealth.RecordFailure("   ", "boom");
        AccountReadHealth.RecordSuccess(string.Empty);

        Assert.False(AccountReadHealth.LastReadFailed(string.Empty));
    }

    // ── Rollup wiring ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AQuietAccountIsNotFlaggedAsUnreadable()
    {
        // Zero conversations, but the read succeeded. This must render "no activity", not a warning.
        AccountReadHealth.RecordSuccess("quiet");

        var entity = BuildOne("quiet");

        Assert.False(entity.ReadFailed);
        Assert.Equal(0, entity.MeasuredCount);   // identical numbers to the broken case below…
    }

    [Fact]
    public void AnUnreadableAccountIsFlagged()
    {
        AccountReadHealth.RecordFailure("broken", "no ingestion path returned usable data");

        var entity = BuildOne("broken");

        Assert.True(entity.ReadFailed);
        Assert.Equal(0, entity.MeasuredCount);   // …which is exactly why the flag has to be carried
    }

    [Fact]
    public void ALocationIsFlaggedWhenAnySingleMemberAccountCannotBeRead()
    {
        // Deliberately ANY, not ALL (unlike IsStale). A location whose three branches include one the app
        // cannot read is reporting incomplete numbers, and requiring all three to fail would hide exactly
        // the case that matters — one branch quietly dropping out of the rollup.
        AccountReadHealth.RecordSuccess("br-1");
        AccountReadHealth.RecordSuccess("br-2");
        AccountReadHealth.RecordFailure("br-3", "boom");

        var snap = OversightRollupBuilder.Build(
            [Thread("br-1"), Thread("br-2"), Thread("br-3")],
            [Inst("br-1"), Inst("br-2"), Inst("br-3")],
            OversightGrouping.ByLocation,
            _ => 15,
            readFailed: AccountReadHealth.LastReadFailed,
            locationForInstance: _ => "Islamabad",
            chatSnapshot: _ => (10, 5));

        Assert.True(snap.Entities.Single().ReadFailed);
    }

    [Fact]
    public void ALocationWhereEveryAccountReadsFineIsNotFlagged()
    {
        AccountReadHealth.RecordSuccess("br-1");
        AccountReadHealth.RecordSuccess("br-2");

        var snap = OversightRollupBuilder.Build(
            [Thread("br-1"), Thread("br-2")],
            [Inst("br-1"), Inst("br-2")],
            OversightGrouping.ByLocation,
            _ => 15,
            readFailed: AccountReadHealth.LastReadFailed,
            locationForInstance: _ => "Islamabad",
            chatSnapshot: _ => (10, 5));

        Assert.False(snap.Entities.Single().ReadFailed);
    }

    [Fact]
    public void OmittingTheResolverLeavesEveryEntityUnflagged()
    {
        // Back-compat: existing callers that pass no readFailed resolver must be unaffected.
        AccountReadHealth.RecordFailure("acct", "boom");

        var snap = OversightRollupBuilder.Build(
            [Thread("acct")], [Inst("acct")], OversightGrouping.ByInstance, _ => 15,
            chatSnapshot: _ => (0, 0));

        Assert.False(snap.Entities.Single().ReadFailed);
    }
}
