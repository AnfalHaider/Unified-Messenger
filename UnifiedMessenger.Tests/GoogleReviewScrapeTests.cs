using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Exercises the Google review-health scrape through the <see cref="IInstanceConnection"/> abstraction (#26):
/// with a fake connection the parser is testable without a live WebView.
/// </summary>
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
