using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-PERF-03 — a scan stage meaning "this page has not loaded yet" must not be treated as a fault.
///
/// <para>
/// Lazy WebView loading is on by default, so a background account's page has never navigated to WhatsApp
/// Web. The injected adapter is therefore absent, <c>indexedDB.open</c> blocks, and the JS watchdog settles
/// at <c>watchdog-timeout</c> after 20 seconds. Observed in the owner's live log on a real WhatsApp
/// account.
/// </para>
/// <para>
/// Before this fix that was recorded as a read failure, rendering "can't read this account — click
/// Re-sync". That advice is wrong twice over: nothing is broken, and Re-sync cannot load a page that lazy
/// loading deliberately left unloaded. Same false-positive class as the Google Business one fixed in
/// v4.99.18, reached by a different route.
/// </para>
/// <para>
/// <b>Why this tests the classifier rather than RefreshAsync:</b> the scan path calls
/// <c>DispatcherQueue.GetForCurrentThread()</c>, which cannot activate in a plain xUnit host — an attempt
/// to test it end-to-end failed with "ClassFactory cannot supply requested class", an environment limit
/// rather than a product behaviour. The classification is the actual decision, so it is tested directly.
/// </para>
/// </summary>
public class NotLoadedIsNotUnreadableTests
{
    [Theory]
    [InlineData("watchdog-timeout")]   // the live case: indexedDB.open never returned
    [InlineData("no-model-storage")]   // WhatsApp Web's database absent — page never loaded it
    [InlineData("no-indexeddb")]
    [InlineData("no-databases-api")]
    [InlineData("databases-rejected")]
    public void StagesMeaningThePageHasNotLoadedAreNotFaults(string stage)
    {
        Assert.True(
            OversightSnapshotReader.IsPageNotReadyStage(stage),
            $"stage '{stage}' means the page never loaded; flagging it as unreadable tells the owner to "
            + "Re-sync, which cannot load a lazily-unloaded page");
    }

    [Theory]
    [InlineData("no-chat-store")]      // page WAS reachable, but the expected store is missing
    [InlineData("getall-chat-error")]
    [InlineData("chat-exception")]
    [InlineData("promise-error")]
    public void StagesMeaningTheReadGenuinelyFailedStillCount(string stage)
    {
        // The guard must not swallow real faults — that would re-create the silence fixed in v4.99.6.
        Assert.False(
            OversightSnapshotReader.IsPageNotReadyStage(stage),
            $"stage '{stage}' is a genuine read failure and must still be flagged");
    }

    [Theory]
    [InlineData("done")]
    [InlineData("empty")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something-new-from-a-future-scraper")]
    public void UnknownOrSuccessfulStagesAreNotTreatedAsNotReady(string? stage)
    {
        // Default to "this counts", so a stage added later is surfaced rather than silently ignored.
        Assert.False(OversightSnapshotReader.IsPageNotReadyStage(stage));
    }
}
