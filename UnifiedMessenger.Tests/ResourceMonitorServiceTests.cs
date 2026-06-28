using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ResourceMonitorServiceTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1_048_576, 1)]
    [InlineData(2_621_440, 2)]
    public void ConvertWorkingSetToMegabytes_TruncatesToWholeMegabytes(long bytes, long expectedMegabytes)
    {
        Assert.Equal(expectedMegabytes, ResourceMonitorService.ConvertWorkingSetToMegabytes(bytes));
    }

    [Theory]
    [InlineData(null, "None")]
    [InlineData("   ", "None")]
    [InlineData("missing", "None")]
    public void ResolveVisibleDisplayName_ReturnsNoneWhenMissing(string? visibleId, string expected)
    {
        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "Personal WhatsApp" }
        };

        Assert.Equal(expected, ResourceMonitorService.ResolveVisibleDisplayName(instances, visibleId));
    }

    [Fact]
    public void ResolveVisibleDisplayName_MatchesTrimmedInstanceId()
    {
        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "Personal WhatsApp" }
        };

        Assert.Equal(
            "Personal WhatsApp",
            ResourceMonitorService.ResolveVisibleDisplayName(instances, "  inst-1  "));
    }

    [Fact]
    public void SumInstanceUnreadCounts_ScopesToProvidedInstances()
    {
        var hub = NotificationHub.CreateForTests();
        hub.UpdateBadgeCount("inst-1", 2);
        hub.UpdateBadgeCount("inst-2", 5);

        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "One" }
        };

        Assert.Equal(2, ResourceMonitorService.SumInstanceUnreadCounts(instances, hub));
    }

    [Fact]
    public void BuildTile_UsesConfiguredMemoryTierAndVisibility()
    {
        var instanceId = $"inst-build-tile-{Guid.NewGuid():N}";
        var hub = NotificationHub.CreateForTests();
        hub.UpdateBadgeCount(instanceId, 3);

        var healthMonitor = AdapterHealthMonitor.Instance;
        healthMonitor.MarkReady(instanceId, "whatsapp");
        healthMonitor.RecordHeartbeat(instanceId, "whatsapp");

        try
        {
            var tile = ResourceMonitorService.BuildTile(
                new MessengerInstance
                {
                    Id = instanceId,
                    DisplayName = "Work",
                    Platform = "whatsapp",
                    AccentColor = "#25D366",
                    IconGlyph = "\uE8BD",
                    MemoryTier = MemoryTierPreference.High
                },
                instanceId,
                hub,
                healthMonitor);

            Assert.True(tile.IsVisible);
            Assert.Equal("High", tile.MemoryTier);
            Assert.Equal(3, tile.UnreadCount);
        }
        finally
        {
            healthMonitor.RemoveInstance(instanceId);
        }
    }

    [Fact]
    public void AggregateWebViewMemory_SumsPositiveSamplesAndCountsThem()
    {
        // A process can exit between enumeration and the WorkingSet64 read → 0/negative samples are skipped.
        var (totalBytes, count) = ResourceMonitorService.AggregateWebViewMemory(
            [100_000_000, 0, 250_000_000, -1, 50_000_000]);

        Assert.Equal(400_000_000, totalBytes);
        Assert.Equal(3, count);
    }

    [Fact]
    public void AggregateWebViewMemory_NoProcesses_ReturnsZero()
    {
        var (totalBytes, count) = ResourceMonitorService.AggregateWebViewMemory([]);

        Assert.Equal(0, totalBytes);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Capture_IncludesWebViewChildProcessesInTotalMemory()
    {
        var hub = NotificationHub.CreateForTests();
        var sessionManager = new InstanceSessionManager();
        var healthMonitor = AdapterHealthMonitor.Instance;

        // App process = 100 MB; three WebView2 children = 200 + 150 + 50 = 400 MB → total 500 MB.
        var service = ResourceMonitorService.CreateForTests(
            workingSetBytesProvider: () => 100L * 1024 * 1024,
            webViewWorkingSetsProvider: () =>
                [200L * 1024 * 1024, 150L * 1024 * 1024, 50L * 1024 * 1024]);

        var snapshot = service.Capture(
            [new MessengerInstance { Id = "inst-1", DisplayName = "One", Platform = "whatsapp" }],
            sessionManager,
            hub,
            healthMonitor);

        Assert.Equal(100, snapshot.WorkingSetMegabytes);
        Assert.Equal(400, snapshot.WebView2WorkingSetMegabytes);
        Assert.Equal(3, snapshot.WebView2ProcessCount);
        Assert.Equal(500, snapshot.TotalWorkingSetMegabytes);
    }

    [Fact]
    public void Capture_OrdersTilesAndUsesInjectedWorkingSet()
    {
        var hub = NotificationHub.CreateForTests();
        hub.UpdateBadgeCount("inst-b", 1);

        var sessionManager = new InstanceSessionManager();
        sessionManager.SetVisibleInstanceForTests("inst-b");

        var healthMonitor = AdapterHealthMonitor.Instance;
        healthMonitor.MarkReady("inst-a", "slack");
        healthMonitor.MarkReady("inst-b", "whatsapp");

        try
        {
            var service = ResourceMonitorService.CreateForTests(() => 5_242_880);
            var snapshot = service.Capture(
                [
                    new MessengerInstance
                    {
                        Id = "inst-b",
                        DisplayName = "Beta",
                        SortOrder = 2,
                        Platform = "whatsapp"
                    },
                    new MessengerInstance
                    {
                        Id = "inst-a",
                        DisplayName = "Alpha",
                        SortOrder = 1,
                        Platform = "slack"
                    },
                    new MessengerInstance { Id = "   ", DisplayName = "Invalid" }
                ],
                sessionManager,
                hub,
                healthMonitor);

            Assert.Equal(2, snapshot.ActiveAccountCount);
            Assert.Equal(1, snapshot.TotalUnreadCount);
            Assert.Equal(5, snapshot.WorkingSetMegabytes);
            Assert.Equal("Beta", snapshot.VisibleInstanceName);
            Assert.Equal(["inst-a", "inst-b"], snapshot.InstanceTiles.Select(tile => tile.InstanceId));
        }
        finally
        {
            healthMonitor.RemoveInstance("inst-a");
            healthMonitor.RemoveInstance("inst-b");
        }
    }
}
