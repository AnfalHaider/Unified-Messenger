using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests.Backfill;

public class MessageAnalyticsBackfillTests : IDisposable
{
    private readonly string _storePath;
    private readonly int _originalSlaThreshold;

    public MessageAnalyticsBackfillTests()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        _storePath = Path.Combine(tempDirectory, "analytics.json");
        _originalSlaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;
    }

    [Fact]
    public void RecordBackfillInbound_CountsReceivedWithoutLivePair()
    {
        var service = new MessageAnalyticsService(_storePath);

        service.RecordBackfillInbound(
            "inst-1",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            AppSettingsService.Instance.Settings.SlaThresholdMinutes);

        Assert.Equal(1, service.GetReceivedCount("inst-1"));
        Assert.Equal(0, service.GetSentCount("inst-1"));
    }

    [Fact]
    public void RecordBackfillDailyAggregate_MergesBucketsWithoutHeadlineTotals()
    {
        var service = new MessageAnalyticsService(_storePath);

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        service.RecordBackfillDailyAggregate(
            "inst-1",
            new Dictionary<string, int> { [today] = 4 },
            new Dictionary<string, int> { [today] = 2 });

        Assert.Equal(0, service.GetReceivedCount("inst-1"));
        Assert.Equal(0, service.GetSentCount("inst-1"));

        var snapshot = service.CaptureProfessionalSnapshot(
            [new Models.MessengerInstance { Id = "inst-1", DisplayName = "Branch", Platform = "whatsapp", Category = Models.WorkspaceCategory.Professional }],
            NotificationHub.Instance);

        var todayPoint = snapshot.WeeklyActivity.FirstOrDefault(point => point.Received >= 4 || point.Sent >= 2);
        Assert.NotNull(todayPoint);
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = _originalSlaThreshold;
    }
}
