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
    public void RecordBackfillInbound_DoesNotAddSlaCandidateWhenMessageIsAged()
    {
        var service = new MessageAnalyticsService(_storePath);

        service.RecordBackfillInbound(
            "inst-1",
            DateTimeOffset.UtcNow.AddHours(-2),
            AppSettingsService.Instance.Settings.SlaThresholdMinutes);

        Assert.Equal(1, service.GetReceivedCount("inst-1"));
        Assert.Equal(0, service.GetSlaBreachCount("inst-1"));
    }

    [Fact]
    public void RecordBackfillInbound_DoesNotDoubleCountSlaWithLiveReceiveSend()
    {
        var service = new MessageAnalyticsService(_storePath);
        var receivedAt = DateTimeOffset.UtcNow.AddHours(-2);

        service.RecordBackfillInbound("inst-1", receivedAt, 15);
        Assert.Equal(0, service.GetSlaBreachCount("inst-1"));

        service.RecordMessageReceived("inst-1");
        service.RecordMessageSent("inst-1");

        Assert.Equal(0, service.GetSlaBreachCount("inst-1"));
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = _originalSlaThreshold;
    }
}
