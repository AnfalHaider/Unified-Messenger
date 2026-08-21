using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The freshness floor that stops the Google review scrape running over and over on startup.
/// </summary>
/// <remarks>
/// <para>
/// Measured before this existed: six scrapes per Google account in the two minutes after launch; one each
/// afterwards. <c>ReviewHealthPanel</c> starts a scrape from its Loaded handler, and the dashboard reloads
/// that panel on every alert-monitor tick and adapter-health change, so each reload fired a fresh pass over
/// every account. The service's SemaphoreSlim only blocks <i>concurrent</i> passes — a pass that finishes in
/// three seconds is not concurrent with the one after it.
/// </para>
/// <para>
/// This is real traffic to a real Google account that can be rate-limited, which is why the rule lives in
/// the service where no caller can bypass it, and why it is pinned here rather than left as a comment.
/// </para>
/// </remarks>
public class ReviewScrapeThrottleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private static bool Skip(DateTimeOffset? last, bool readData, bool force = false) =>
        GoogleReviewSnapshotService.ShouldSkipAsTooRecent(last, readData, Now, force);

    [Fact]
    public void AnAccountNeverScrapedIsAlwaysRead() =>
        Assert.False(Skip(last: null, readData: false));

    [Fact]
    public void ASecondPassMomentsAfterAGoodReadIsSkipped() =>
        // The startup case: panel loads, dashboard redraws, panel loads again.
        Assert.True(Skip(Now.AddSeconds(-3), readData: true));

    [Fact]
    public void OnceTheFloorHasPassedTheScrapeRunsAgain() =>
        Assert.False(Skip(Now - GoogleReviewSnapshotService.MinimumRescrapeInterval, readData: true));

    [Fact]
    public void TheOwnerDrivenResyncIsNeverThrottled() =>
        // "Re-sync now" that answers with a cached number is a broken button, however recent the data is.
        Assert.False(Skip(Now.AddSeconds(-1), readData: true, force: true));

    [Fact]
    public void ThePanelsOwnFiveMinuteTimerAlwaysGetsThrough()
    {
        // The floor is deliberately below ReviewHealthPanel's 5-minute auto-refresh so the periodic read
        // still happens every time and only the incidental re-entries around it collapse. Raise the floor
        // past 5 minutes and that timer silently becomes a no-op — hence this test.
        Assert.True(
            GoogleReviewSnapshotService.MinimumRescrapeInterval < TimeSpan.FromMinutes(5),
            "The freshness floor must stay below the panel's 5-minute auto-refresh, or that timer becomes a no-op.");

        Assert.False(Skip(Now.AddMinutes(-5), readData: true));
    }

    [Fact]
    public void AClockThatWentBackwardsDoesNotSilenceTheScrape() =>
        // NTP correction, a DST edge, a VM resuming from suspend. Without this the scrape would go quiet
        // until real time caught up with the stamp, which could be hours.
        Assert.False(Skip(Now.AddMinutes(30), readData: true));

    // ---- an attempt that read nothing retries sooner -------------------------------------------------

    [Fact]
    public void AFailedAttemptIsRetriedLongBeforeAGoodOneIs()
    {
        // Verified live: on a cold start the first scrape often fails — the WebView has not run the injected
        // reader yet, or the account is the one on screen and the pass may not navigate. Holding those for
        // the full four minutes leaves the Reviews card empty that whole time.
        Assert.True(GoogleReviewSnapshotService.FailedRetryInterval < GoogleReviewSnapshotService.MinimumRescrapeInterval);

        var oneMinuteAgo = Now.AddMinutes(-1);
        Assert.False(Skip(oneMinuteAgo, readData: false)); // failed → retry now
        Assert.True(Skip(oneMinuteAgo, readData: true));   // succeeded → wait
    }

    [Fact]
    public void AFailingAccountIsStillNotScrapedOnEveryPanelReload() =>
        // The point of throttling the ATTEMPT rather than the cached result: without this, an account that
        // is failing would be the one account still scraped every few seconds — the worst case for traffic
        // and the least likely to recover because of it.
        Assert.True(Skip(Now.AddSeconds(-5), readData: false));
}
