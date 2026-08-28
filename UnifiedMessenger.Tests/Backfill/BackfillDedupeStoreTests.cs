using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests.Backfill;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class BackfillDedupeStoreTests : IDisposable
{
    private readonly string _storePath;

    public BackfillDedupeStoreTests()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        _storePath = Path.Combine(tempDirectory, "backfill_dedupe.json");
    }

    [Fact]
    public async Task TryAcceptForDayAsync_SuppressesSameConversationOnSameDay()
    {
        var store = new BackfillDedupeStore(_storePath);
        var timestamp = LocalMorning(DateTime.Today);

        Assert.True(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", timestamp));
        Assert.False(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", timestamp.AddHours(2)));
    }

    [Fact]
    public async Task TryAcceptForDayAsync_AllowsSameConversationOnDifferentDay()
    {
        var store = new BackfillDedupeStore(_storePath);
        var dayOne = LocalMorning(DateTime.Today.AddDays(-1));
        var dayTwo = LocalMorning(DateTime.Today);

        Assert.True(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", dayOne));
        Assert.True(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", dayTwo));
    }

    /// <summary>
    /// 09:00 on the given LOCAL day, as the offset that day actually had.
    /// </summary>
    /// <remarks>
    /// Both constraints below are load-bearing, and the two tests in this file each used to violate one.
    /// <list type="bullet">
    /// <item><b>Recent.</b> <c>BackfillDedupeStore.PruneStaleEntries</c> drops anything older than 45 days
    /// and runs on every save, so a fixed calendar date goes stale and is discarded the moment it is
    /// written. <c>AllowsSameConversationOnDifferentDay</c> used 2026-06-10/11, which by 2026-08-28 was 79
    /// days old: both entries were pruned, both calls trivially returned true, and the test passed without
    /// exercising day-keying at all. It would have passed with dedupe entirely removed.</item>
    /// <item><b>Mid-morning local.</b> The key is the LOCAL day (the v4.99.50 fix, so it agrees with the
    /// analytics bucket). <c>SuppressesSameConversationOnSameDay</c> used <c>UtcNow</c> and added two hours,
    /// which is the same local day only when UtcNow is more than two hours from local midnight. On this
    /// machine (UTC+5) it went red at 22:00 local and would have gone green again at midnight — red for two
    /// hours of every day, on CI as much as here, which reads as flake rather than as a broken test.</item>
    /// </list>
    /// </remarks>
    private static DateTimeOffset LocalMorning(DateTime localDay)
    {
        var at9 = DateTime.SpecifyKind(localDay.Date.AddHours(9), DateTimeKind.Unspecified);
        return new DateTimeOffset(at9, TimeZoneInfo.Local.GetUtcOffset(at9));
    }

    public void Dispose()
    {
        if (File.Exists(_storePath))
        {
            File.Delete(_storePath);
        }
    }
}
