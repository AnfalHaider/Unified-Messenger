using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression cover for the metric-honesty fixes in the 2026-08-26 audit, phase 3.
/// </summary>
/// <remarks>
/// Shares the "StoreBridgeHealth" collection with <see cref="StoreBridgeHealthTests"/>. Both classes
/// <c>Reset()</c> and <c>Record()</c> into the same process-wide static, and xUnit runs test *classes* in
/// parallel — so they raced, and the counts one asserted on included rows the other had just written. It
/// stayed hidden until unrelated new tests changed the scheduling, which is how a latent flake usually
/// announces itself. Same collection means never concurrent.
/// </remarks>
[Collection("StoreBridgeHealth")]
public class Phase3HonestyTests : IDisposable
{
    private readonly string _tempDir;

    public Phase3HonestyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        StoreBridgeHealth.Reset();
    }

    public void Dispose()
    {
        StoreBridgeHealth.Reset();
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // A locked temp file must not fail the suite.
        }

        GC.SuppressFinalize(this);
    }

    private static MessengerInstance Inst(string id) =>
        new() { Id = id, DisplayName = id, Platform = "whatsapp" };

    private static StoreBridgeHealth.Entry Entry(bool succeeded) =>
        new(succeeded, "done", "modulesMap", 100, 80, DateTimeOffset.UtcNow);

    // ── S2-16: the missed-call count is over-stated on the IndexedDB fallback ────────────────────

    [Fact]
    public void AnyAccountOnFallback_IsFalseBeforeAnythingHasBeenProbed() =>
        Assert.False(StoreBridgeHealth.AnyAccountOnFallback);

    [Fact]
    public void AnyAccountOnFallback_IsFalseWhenTheBridgeWorksEverywhere()
    {
        StoreBridgeHealth.Record("a", Entry(succeeded: true));
        StoreBridgeHealth.Record("b", Entry(succeeded: true));

        Assert.False(StoreBridgeHealth.AnyAccountOnFallback);
    }

    [Fact]
    public void AnyAccountOnFallback_IsTrueWhenEvenOneAccountDegraded()
    {
        StoreBridgeHealth.Record("a", Entry(succeeded: true));
        StoreBridgeHealth.Record("b", Entry(succeeded: false));

        Assert.True(StoreBridgeHealth.AnyAccountOnFallback);
    }

    // ── S3-14: sparklines must not plot readings weeks apart as adjacent points ──────────────────

    [Fact]
    public void KpiTrend_StopsAtAGapRatherThanPlottingDistantDaysAsAdjacent()
    {
        var path = Path.Combine(_tempDir, "kpi-trend.json");
        var store = new KpiTrendStore(path);

        // Today and yesterday have readings; the day before does not.
        store.RecordForTests(DateTime.Now.Date.AddDays(-4), caughtUpPercent: 10, awaiting: 40);
        store.RecordForTests(DateTime.Now.Date.AddDays(-1), caughtUpPercent: 80, awaiting: 5);
        store.RecordForTests(DateTime.Now.Date, caughtUpPercent: 90, awaiting: 2);

        var trend = store.GetCaughtUpTrend(14);

        // The 4-days-ago reading of 10 is behind a gap. Including it would draw a steep climb across
        // three "adjacent" points that actually span most of a week.
        Assert.Equal([80, 90], trend);
    }

    [Fact]
    public void KpiTrend_ReturnsTheWholeRunWhenEveryDayHasAReading()
    {
        var path = Path.Combine(_tempDir, "kpi-trend-full.json");
        var store = new KpiTrendStore(path);

        store.RecordForTests(DateTime.Now.Date.AddDays(-2), caughtUpPercent: 50, awaiting: 20);
        store.RecordForTests(DateTime.Now.Date.AddDays(-1), caughtUpPercent: 70, awaiting: 10);
        store.RecordForTests(DateTime.Now.Date, caughtUpPercent: 90, awaiting: 2);

        Assert.Equal([50, 70, 90], store.GetCaughtUpTrend(14));
    }

    [Fact]
    public void KpiTrend_IsEmptyWhenTodayHasNoReading()
    {
        var path = Path.Combine(_tempDir, "kpi-trend-stale.json");
        var store = new KpiTrendStore(path);

        store.RecordForTests(DateTime.Now.Date.AddDays(-3), caughtUpPercent: 60, awaiting: 12);

        // Nothing recorded today means no current run — better an absent sparkline than one implying
        // the last reading is current.
        Assert.Empty(store.GetCaughtUpTrend(14));
    }

    // ── S3-13: "answered today" is about today, not about the selected range ─────────────────────

    [Fact]
    public void AnsweredToday_SurvivesADateRangeThatEndsBeforeNow()
    {
        var tracker = new ResponseTimeTracker(Path.Combine(_tempDir, "response-times.json"));
        tracker.SetWatchStartForTests("inst-1", DateTimeOffset.UtcNow.AddDays(-30));

        var inbound = DateTimeOffset.UtcNow.AddMinutes(-30);
        tracker.Observe("inst-1", "chat-a", isAwaiting: true, lastMessageFromMe: false, inbound);
        tracker.Observe("inst-1", "chat-a", isAwaiting: false, lastMessageFromMe: true, inbound.AddMinutes(5));

        // The owner is looking at last week. The reply happened today, and the chip says "today".
        var stats = tracker.GetStats(
            [Inst("inst-1")],
            fromUtc: DateTimeOffset.UtcNow.AddDays(-14),
            toUtc: DateTimeOffset.UtcNow.AddDays(-7),
            slaThresholdMinutes: 15);

        Assert.Equal(1, stats.AnsweredToday);

        // ...and the range still governs everything that is genuinely range-scoped.
        Assert.Equal(0, stats.SampleCount);
    }

    [Fact]
    public void AnsweredToday_CountsTodayWhenTheRangeIsWideOpen()
    {
        var tracker = new ResponseTimeTracker(Path.Combine(_tempDir, "response-times-2.json"));
        tracker.SetWatchStartForTests("inst-1", DateTimeOffset.UtcNow.AddDays(-30));

        var inbound = DateTimeOffset.UtcNow.AddMinutes(-30);
        tracker.Observe("inst-1", "chat-a", isAwaiting: true, lastMessageFromMe: false, inbound);
        tracker.Observe("inst-1", "chat-a", isAwaiting: false, lastMessageFromMe: true, inbound.AddMinutes(5));

        var stats = tracker.GetStats([Inst("inst-1")], null, null, slaThresholdMinutes: 15);

        Assert.Equal(1, stats.AnsweredToday);
        Assert.Equal(1, stats.SampleCount);
    }
}
