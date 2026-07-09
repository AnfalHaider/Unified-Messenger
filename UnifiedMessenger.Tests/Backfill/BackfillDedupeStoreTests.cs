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
        var timestamp = DateTimeOffset.UtcNow;

        Assert.True(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", timestamp));
        Assert.False(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", timestamp.AddHours(2)));
    }

    [Fact]
    public async Task TryAcceptForDayAsync_AllowsSameConversationOnDifferentDay()
    {
        var store = new BackfillDedupeStore(_storePath);
        var dayOne = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
        var dayTwo = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

        Assert.True(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", dayOne));
        Assert.True(await store.TryAcceptForDayAsync("inst-1", "whatsapp", "Sara", dayTwo));
    }

    public void Dispose()
    {
        if (File.Exists(_storePath))
        {
            File.Delete(_storePath);
        }
    }
}
