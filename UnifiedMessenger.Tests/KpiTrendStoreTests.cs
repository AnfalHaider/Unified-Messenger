using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class KpiTrendStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;

    public KpiTrendStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "kpi-trend.json");
    }

    [Fact]
    public void Record_OverwritesTodaysValue_AndSeriesReturnsIt()
    {
        var store = new KpiTrendStore(_storePath);
        store.Record(80, 5);
        store.Record(90, 2); // same day → overwrites

        var caughtUp = store.GetCaughtUpTrend(7);
        var awaiting = store.GetAwaitingTrend(7);

        // Only today has data, so one point each, reflecting the latest write.
        Assert.Equal(90, Assert.Single(caughtUp));
        Assert.Equal(2, Assert.Single(awaiting));
    }

    [Fact]
    public void Record_ClampsPercentAndFloorsAwaiting()
    {
        var store = new KpiTrendStore(_storePath);
        store.Record(150, -3);

        Assert.Equal(100, Assert.Single(store.GetCaughtUpTrend(7)));
        Assert.Equal(0, Assert.Single(store.GetAwaitingTrend(7)));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
