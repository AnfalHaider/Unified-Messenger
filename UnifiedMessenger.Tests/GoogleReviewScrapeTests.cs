using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Exercises the Google review-health scrape through the <see cref="IInstanceConnection"/> abstraction (#26):
/// with a fake connection the parser is testable without a live WebView.
/// </summary>
[Collection("InstanceConnection")]
public class GoogleReviewScrapeTests
{
    private sealed class FakeConnection : IInstanceConnection
    {
        private readonly string? _readResult;

        // innerJson is what the page's read script would yield; ExecuteScriptAsync returns it JSON-encoded
        // (as WebView2's ExecuteScriptAsync wraps a JS string result), which is what the service unwraps.
        public FakeConnection(string? innerJson) =>
            _readResult = innerJson is null ? null : JsonSerializer.Serialize(innerJson);

        public Task<string?> ExecuteScriptAsync(string instanceId, string script) =>
            Task.FromResult(script.TrimStart().StartsWith("(window.__umGR", StringComparison.Ordinal) ? _readResult : null);

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
}
