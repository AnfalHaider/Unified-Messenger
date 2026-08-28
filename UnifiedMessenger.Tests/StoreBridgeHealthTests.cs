using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection("StoreBridgeHealth")]
public class StoreBridgeHealthTests
{
    public StoreBridgeHealthTests() => StoreBridgeHealth.Reset();

    [Fact]
    public void Describe_ReportsNotProbedBeforeAnyAttempt()
    {
        Assert.Contains("Not yet probed", StoreBridgeHealth.Describe(), StringComparison.Ordinal);
        Assert.Equal(0, StoreBridgeHealth.AttemptedCount);
        Assert.Null(StoreBridgeHealth.LastSuccessUtc);
    }

    [Fact]
    public void Describe_NamesTheFailureStageWhenTheBridgeIsUnavailable()
    {
        StoreBridgeHealth.Record("acct-1", Failed("no-store"));

        var description = StoreBridgeHealth.Describe();

        // The stage is the only clue for tuning discovery against a live account, so it must survive
        // into the UI rather than collapsing to a generic "unavailable".
        Assert.Contains("no-store", description, StringComparison.Ordinal);
        Assert.Contains("IndexedDB", description, StringComparison.Ordinal);
        Assert.Equal(0, StoreBridgeHealth.ActiveCount);
    }

    [Fact]
    public void Describe_CountsActiveAccountsAgainstAttempted()
    {
        StoreBridgeHealth.Record("acct-1", Succeeded());
        StoreBridgeHealth.Record("acct-2", Succeeded());
        StoreBridgeHealth.Record("acct-3", Failed("not-injected"));

        var description = StoreBridgeHealth.Describe();

        Assert.Contains("Active on 2 of 3 accounts", description, StringComparison.Ordinal);
        Assert.Equal(2, StoreBridgeHealth.ActiveCount);
        Assert.Equal(3, StoreBridgeHealth.AttemptedCount);
        Assert.NotNull(StoreBridgeHealth.LastSuccessUtc);
    }

    [Fact]
    public void Record_LatestAttemptPerInstanceWins()
    {
        StoreBridgeHealth.Record("acct-1", Succeeded());
        StoreBridgeHealth.Record("acct-1", Failed("parse-error"));

        Assert.Equal(1, StoreBridgeHealth.AttemptedCount);
        Assert.Equal(0, StoreBridgeHealth.ActiveCount);
        Assert.Equal("parse-error", StoreBridgeHealth.TryGet("acct-1")!.Value.Stage);
    }

    [Fact]
    public void TryGet_IsCaseAndWhitespaceInsensitive()
    {
        StoreBridgeHealth.Record("Acct-1", Succeeded());

        Assert.NotNull(StoreBridgeHealth.TryGet(" acct-1 "));
        Assert.Null(StoreBridgeHealth.TryGet("acct-2"));
        Assert.Null(StoreBridgeHealth.TryGet(""));
    }

    private static StoreBridgeHealth.Entry Succeeded() =>
        new(true, "done", "debug-require", Conversations: 12, WithPreview: 11, DateTimeOffset.UtcNow);

    private static StoreBridgeHealth.Entry Failed(string stage) =>
        new(false, stage, string.Empty, Conversations: 0, WithPreview: 0, DateTimeOffset.UtcNow);
}
