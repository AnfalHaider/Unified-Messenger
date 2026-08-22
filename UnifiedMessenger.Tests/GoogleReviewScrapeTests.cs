using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Exercises the Google review-health scrape through the <see cref="IInstanceConnection"/> abstraction (#26):
/// with a fake connection the parser is testable without a live WebView.
/// </summary>
[Collection("InstanceConnection")]
public class GoogleReviewScrapeTests : IDisposable
{
    private readonly ReviewHistoryStore _originalHistory = ReviewHistory.Current;
    private readonly string _historyPath = Path.Combine(
        Path.GetTempPath(), $"um-scrape-history-{Guid.NewGuid():N}.json");

    /// <summary>
    /// Point the scrape's history writes at a throwaway file.
    /// </summary>
    /// <remarks>
    /// <b>This is not tidiness.</b> ScrapeAsync records a daily reading, and without this the singleton store
    /// wrote entries for the fake ids below straight into
    /// <c>%LOCALAPPDATA%\UnifiedMessenger\review-history.json</c> — a real test run overwrote a real day of
    /// the owner's review history with "g-review-1". A test must never be able to reach live business data.
    /// </remarks>
    public GoogleReviewScrapeTests() => ReviewHistory.Current = new ReviewHistoryStore(_historyPath);

    public void Dispose()
    {
        ReviewHistory.Current = _originalHistory;
        if (File.Exists(_historyPath))
        {
            File.Delete(_historyPath);
        }
    }

    [Fact]
    public async Task ScrapeAsync_RecordsItsReadingIntoTheAmbientHistoryStore()
    {
        // Also the guard on the isolation above: if the scrape ever stops writing through
        // ReviewHistory.Current, this fails rather than silently resuming writes to the real file.
        var original = InstanceConnection.Current;
        InstanceConnection.Current = new FakeConnection("{\"state\":\"done\",\"unanswered\":2,\"answered\":8}");
        try
        {
            await GoogleReviewSnapshotService.Instance.ScrapeAsync("g-review-history");

            var day = Assert.Single(ReviewHistory.Current.GetHistory("g-review-history"));
            Assert.Equal(2, day.Unanswered);
            Assert.Equal(8, day.Answered);

            // The reviews scrape does not read the rating, and must not record a zero for it.
            Assert.Null(day.Rating);
            Assert.Null(day.LifetimeTotal);
        }
        finally
        {
            InstanceConnection.Current = original;
        }
    }

    private sealed class FakeConnection : IInstanceConnection
    {
        private readonly string? _readResult;

        // innerJson is what the page's read script would yield; ExecuteScriptAsync returns it JSON-encoded
        // (as WebView2's ExecuteScriptAsync wraps a JS string result), which is what the service unwraps.
        public FakeConnection(string? innerJson) =>
            _readResult = innerJson is null ? null : JsonSerializer.Serialize(innerJson);

        public Task<string?> ExecuteScriptAsync(string instanceId, string script)
        {
            // The service asks "1" first to find out whether the page can run scripts at all, so it can skip
            // a sleeping WebView instead of polling it for 60 seconds. This fake stands in for a live page,
            // so it answers — a fake that returned null here would be modelling a suspended session, and
            // every scrape test would be asserting against a code path that never reaches the parser.
            if (script.Trim() == "1")
            {
                return Task.FromResult<string?>("1");
            }

            return Task.FromResult(
                script.TrimStart().StartsWith("(window.__umGR", StringComparison.Ordinal) ? _readResult : null);
        }

        public Task ReloadAsync(string instanceId) => Task.CompletedTask;
    }

    [Fact]
    public async Task ScrapeAsync_ParsesReplyAndEditCounts()
    {
        var original = InstanceConnection.Current;
        InstanceConnection.Current = new FakeConnection("{\"state\":\"done\",\"unanswered\":1,\"answered\":9}");
        try
        {
            var health = await GoogleReviewSnapshotService.Instance.ScrapeAsync("g-review-1");

            Assert.NotNull(health);
            Assert.True(health!.Value.HasData);
            Assert.Equal(1, health.Value.Unanswered);
            Assert.Equal(9, health.Value.Answered);
            Assert.Equal(10, health.Value.Total);
            Assert.Equal(90, health.Value.ReplyRatePercent);
        }
        finally
        {
            InstanceConnection.Current = original;
        }
    }

    [Fact]
    public async Task ScrapeAsync_ParsesPendingReviewDetail()
    {
        var original = InstanceConnection.Current;
        InstanceConnection.Current = new FakeConnection(
            "{\"state\":\"done\",\"unanswered\":2,\"answered\":0,\"pending\":[" +
            "{\"reviewer\":\"Ayesha K\",\"text\":\"Staff were lovely but the wait was long.\",\"stars\":3,\"age\":\"2 days ago\",\"idx\":0}," +
            // Degraded row: the page yielded no stars/age. It must still list, not be dropped.
            "{\"reviewer\":\"\",\"text\":\"No name rendered\",\"stars\":0,\"age\":\"\",\"idx\":1}]}");
        try
        {
            var health = await GoogleReviewSnapshotService.Instance.ScrapeAsync("g-review-3");

            Assert.NotNull(health);
            Assert.Equal(2, health!.Value.Pending.Count);

            var first = health.Value.Pending[0];
            Assert.Equal("Ayesha K", first.Reviewer);
            Assert.Equal("Staff were lovely but the wait was long.", first.Text);
            Assert.Equal(3, first.Stars);
            Assert.Equal("2 days ago", first.Age);
            Assert.Equal(0, first.Index);

            var second = health.Value.Pending[1];
            Assert.Equal("Reviewer", second.Reviewer);
            Assert.Equal(0, second.Stars);
            Assert.Equal(1, second.Index);
        }
        finally
        {
            InstanceConnection.Current = original;
        }
    }

    [Fact]
    public async Task ScrapeAsync_NotReviewsPage_ReturnsNull()
    {
        var original = InstanceConnection.Current;
        InstanceConnection.Current = new FakeConnection("{\"state\":\"notreviews\"}");
        try
        {
            var health = await GoogleReviewSnapshotService.Instance.ScrapeAsync("g-review-2");
            Assert.Null(health);
        }
        finally
        {
            InstanceConnection.Current = original;
        }
    }

    /// <summary>A session that cannot run scripts is abandoned immediately, not polled for a minute.</summary>
    private sealed class SleepingConnection : IInstanceConnection
    {
        public int Calls { get; private set; }

        // A suspended WebView executes nothing, so every script comes back null.
        public Task<string?> ExecuteScriptAsync(string instanceId, string script)
        {
            Calls++;
            return Task.FromResult<string?>(null);
        }

        public Task ReloadAsync(string instanceId) => Task.CompletedTask;
    }

    [Fact]
    public async Task ScrapeAsync_GivesUpOnASleepingSessionWithinItsReadyBudget()
    {
        // Measured live before this: every account, every 30-minute pass, sat in state 'none' for the full
        // 60-second budget because the WebView was suspended — six minutes of polling dead views per hour,
        // producing nothing.
        //
        // It does NOT give up instantly. Waking a WebView is asynchronous, and an immediate one-shot probe
        // failed all three accounts at 14:00:23 while all three were running scripts by 14:00:36 — skipping
        // precisely the accounts the wake exists to rescue. So the contract is "bounded", not "instant":
        // wait briefly for a session that is coming up, then leave.
        var original = InstanceConnection.Current;
        var originalBudget = GoogleReviewSnapshotService.ScriptReadyBudget;
        var sleeping = new SleepingConnection();
        InstanceConnection.Current = sleeping;
        GoogleReviewSnapshotService.ScriptReadyBudget = TimeSpan.FromSeconds(1);
        try
        {
            var started = DateTimeOffset.UtcNow;
            var health = await GoogleReviewSnapshotService.Instance.ScrapeAsync("g-review-asleep");
            var elapsed = DateTimeOffset.UtcNow - started;

            Assert.Null(health);

            // Bounded by the budget, and nowhere near the 60-second poll loop this replaced — that path
            // cannot finish this quickly, so re-entering it would fail here loudly.
            Assert.True(
                elapsed < TimeSpan.FromSeconds(10),
                $"A sleeping session should be abandoned within its budget; this took {elapsed.TotalSeconds:0.0}s.");

            // It probed, retried while waiting, and never reached the kickoff/reset/read loop — which would
            // run far more calls than a 1-second budget at 500ms between tries can produce.
            Assert.InRange(sleeping.Calls, 1, 6);
        }
        finally
        {
            InstanceConnection.Current = original;
            GoogleReviewSnapshotService.ScriptReadyBudget = originalBudget;
        }
    }
}
