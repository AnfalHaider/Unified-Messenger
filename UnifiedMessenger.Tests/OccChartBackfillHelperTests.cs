using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class OccChartBackfillHelperTests : IDisposable
{
    private readonly IReadOnlyList<ThreadData> _originalThreads;

    public OccChartBackfillHelperTests()
    {
        BackfillSyncManager.Instance.ResetStateForTests();
        AppSettingsService.Instance.Settings.EnableStartupBackfill = true;
        ThreadRegistryService.Instance.RestoreThreads([]);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
    }

    public void Dispose()
    {
        BackfillSyncManager.Instance.ResetStateForTests();
        ThreadRegistryService.Instance.RestoreThreads(_originalThreads);
    }

    [Fact]
    public void HasEmptyChartBuckets_DetectsAllZeroSeries()
    {
        var series = new[]
        {
            new DailyActivityPoint { Label = "Mon", Sent = 0, Received = 0 },
            new DailyActivityPoint { Label = "Tue", Sent = 0, Received = 0 }
        };

        Assert.True(OccChartBackfillHelper.HasEmptyChartBuckets(series));
        Assert.True(OccChartBackfillHelper.HasEmptyChartBuckets(null));
        Assert.False(OccChartBackfillHelper.HasEmptyChartBuckets(
        [
            new DailyActivityPoint { Label = "Mon", Sent = 1, Received = 0 }
        ]));
    }

    [Fact]
    public void ResolveConnectedWhatsAppInstances_FiltersByConnectionAndPlatform()
    {
        var connected = new MessengerInstance
        {
            Id = "wa-connected",
            DisplayName = "Connected",
            Platform = "whatsapp",
            Category = WorkspaceCategory.Professional
        };
        var disconnected = new MessengerInstance
        {
            Id = "wa-offline",
            DisplayName = "Offline",
            Platform = "whatsappbusiness",
            Category = WorkspaceCategory.Professional
        };

        InstanceConnectionStatusService.Instance.SetConnected(connected.Id);

        var resolved = OccChartBackfillHelper.ResolveConnectedWhatsAppInstances(
            [connected, disconnected],
            selectedBranchKey: null);

        Assert.Single(resolved);
        Assert.Equal(connected.Id, resolved[0].Id);
    }

    [Fact]
    public void TryScheduleBackfillForEmptyChart_SchedulesConnectedWhatsAppInstances()
    {
        var instance = new MessengerInstance
        {
            Id = "wa-1",
            DisplayName = "Depilex DHA-2",
            Platform = "whatsapp",
            Category = WorkspaceCategory.Professional
        };

        InstanceConnectionStatusService.Instance.SetConnected(instance.Id);

        OccChartBackfillHelper.TryScheduleBackfillForEmptyChart(
            [],
            [instance],
            selectedBranchKey: null);

        var state = BackfillSyncManager.Instance.GetState(instance.Id);
        Assert.NotEqual(BackfillSyncState.Skipped, state);
    }

    [Fact]
    public void BackfillDailyAggregate_PopulatesChartWithoutChangingLiveKpiCounts()
    {
        var storePath = Path.Combine(
            Path.GetTempPath(),
            "UnifiedMessengerTests",
            Guid.NewGuid().ToString("N"),
            "analytics.json");
        Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);

        var analytics = new MessageAnalyticsService(storePath);
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        analytics.RecordBackfillDailyAggregate(
            "inst-1",
            new Dictionary<string, int> { [today] = 3 },
            new Dictionary<string, int> { [today] = 2 });

        var from = OccDateRangeFilterState.NormalizeStartOfDay(DateTimeOffset.Now)!.Value;
        var to = OccDateRangeFilterState.NormalizeEndOfDay(DateTimeOffset.Now)!.Value;
        var chartSnapshot = analytics.CaptureProfessionalSnapshot(
            [
                new MessengerInstance
                {
                    Id = "inst-1",
                    DisplayName = "Branch",
                    Platform = "whatsapp",
                    Category = WorkspaceCategory.Professional
                }
            ],
            NotificationHub.Instance,
            selectedBranchKey: null,
            fromUtc: from,
            toUtc: to);

        Assert.True(chartSnapshot.HasMessageVolume);
        Assert.Equal(0, analytics.GetReceivedCount("inst-1"));
        Assert.Equal(0, analytics.GetSentCount("inst-1"));

        ThreadRegistryService.Instance.UpsertFromTriageItem(
            new MessageTriageItem
            {
                Id = "inst-1|live",
                InstanceId = "inst-1",
                InstanceDisplayName = "Branch",
                Platform = "whatsapp",
                MessagePreview = "Live queue thread",
                CustomerName = "Live Customer",
                UrgencyScore = 40,
                Sentiment = MessageSentiment.Neutral,
                TimestampUtc = DateTimeOffset.UtcNow.AddDays(-45)
            },
            "Live Customer",
            "DHA-2");

        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "inst-1",
                DisplayName = "Branch",
                Platform = "whatsapp",
                Category = WorkspaceCategory.Professional
            }
        };

        var occ = OperationsCommandCenterService.Instance;
        var liveOccSnapshot = occ.BuildSnapshot(
            instances,
            selectedBranchKey: null,
            from,
            to,
            OccViewMode.Live);

        Assert.Equal(1, liveOccSnapshot.ThreadOperations.OpenThreadCount);

        var historicalOccSnapshot = occ.BuildSnapshot(
            instances,
            selectedBranchKey: null,
            from,
            to,
            OccViewMode.Historical);

        Assert.Equal(0, historicalOccSnapshot.ThreadOperations.OpenThreadCount);
    }
}
