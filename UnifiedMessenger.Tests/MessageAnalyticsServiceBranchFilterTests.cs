using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class MessageAnalyticsServiceBranchFilterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public MessageAnalyticsServiceBranchFilterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "analytics.json");
    }

    [Fact]
    public void CaptureProfessionalSnapshot_AllBranches_AggregatesCounts()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("f11");
        service.RecordMessageReceived("f11");
        service.RecordMessageReceived("d12");
        service.RecordMessageSent("d12");

        var instances = CreateProfessionalBranches();
        var hub = NotificationHub.CreateForTests();

        var snapshot = service.CaptureProfessionalSnapshot(instances, hub);

        Assert.Null(snapshot.FilteredBranchKey);
        Assert.Equal(2, snapshot.IncludedInstanceIds.Count);
        Assert.Equal(3, snapshot.ReceivedCount);
        Assert.Equal(1, snapshot.SentCount);
    }

    [Fact]
    public void CaptureProfessionalSnapshot_SingleBranch_FiltersVolumeAndIds()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("f11");
        service.RecordMessageReceived("f11");
        service.RecordMessageReceived("d12");
        service.RecordMessageSent("d12");

        var instances = CreateProfessionalBranches();
        var hub = NotificationHub.CreateForTests();

        var snapshot = service.CaptureProfessionalSnapshot(instances, hub, "F-11");

        Assert.Equal("F-11", snapshot.FilteredBranchKey);
        Assert.Single(snapshot.IncludedInstanceIds);
        Assert.Equal("f11", snapshot.IncludedInstanceIds[0]);
        Assert.Equal(2, snapshot.ReceivedCount);
        Assert.Equal(0, snapshot.SentCount);
    }

    [Fact]
    public void CaptureProfessionalSnapshot_SingleBranch_FiltersWeeklyActivitySeries()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("f11");
        service.RecordMessageReceived("d12");
        service.RecordMessageReceived("d12");

        var instances = CreateProfessionalBranches();
        var hub = NotificationHub.CreateForTests();

        var all = service.CaptureProfessionalSnapshot(instances, hub);
        var d12Only = service.CaptureProfessionalSnapshot(instances, hub, "Depilex D-12");

        var allReceived = all.WeeklyActivity.Sum(point => point.Received);
        var d12Received = d12Only.WeeklyActivity.Sum(point => point.Received);

        Assert.True(allReceived >= d12Received);
        Assert.Equal(2, d12Received);
        Assert.Single(d12Only.IncludedInstanceIds);
    }

    [Fact]
    public void CaptureProfessionalDashboardTelemetry_AlignsSnapshotAndFilteredInstances()
    {
        var service = new MessageAnalyticsService(_storePath);
        service.RecordMessageReceived("f11");
        service.RecordMessageReceived("d12");

        var instances = CreateProfessionalBranches();
        var hub = NotificationHub.CreateForTests();
        var snapshot = service.CaptureProfessionalSnapshot(instances, hub, "Depilex D-12");
        var telemetry = new ProfessionalDashboardTelemetry
        {
            Snapshot = snapshot,
            Display = DashboardPageHelper.BuildProfessionalDisplay(snapshot),
            FilteredInstances = DashboardPageHelper
                .FilterProfessionalInstances(instances, "Depilex D-12")
                .ToList()
        };

        Assert.Single(telemetry.FilteredInstances);
        Assert.Equal("d12", telemetry.FilteredInstances[0].Id);
        Assert.Equal(1, telemetry.Snapshot.ReceivedCount);
        Assert.Equal("1", telemetry.Display.ReceivedCount);
        Assert.Equal("Depilex D-12", telemetry.Snapshot.FilteredBranchKey);
    }

    private static MessengerInstance[] CreateProfessionalBranches() =>
    [
        new MessengerInstance
        {
            Id = "f11",
            DisplayName = "Depilex F-11",
            Platform = "whatsappbusiness",
            Category = WorkspaceCategory.Professional
        },
        new MessengerInstance
        {
            Id = "d12",
            DisplayName = "Depilex D-12",
            Platform = "whatsapp",
            Category = WorkspaceCategory.Professional
        }
    ];

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
