using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class PersonalDashboardServiceTests
{
    [Fact]
    public void GetPersonalAlertsSortedByRecency_OrdersNewestFirstAcrossInstances()
    {
        var hub = NotificationHub.CreateForTests();
        hub.AddAlert(new NotificationAlert
        {
            Id = "alert-old",
            InstanceId = "inst-a",
            InstanceDisplayName = "Alpha",
            Platform = "slack",
            Title = "Older",
            ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        hub.AddAlert(new NotificationAlert
        {
            Id = "alert-new",
            InstanceId = "inst-b",
            InstanceDisplayName = "Beta",
            Platform = "telegram",
            Title = "Newer",
            ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        var personalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "inst-a", "inst-b" };
        var sorted = PersonalDashboardService.GetPersonalAlertsSortedByRecency(hub, personalIds);

        Assert.Equal(["alert-new", "alert-old"], sorted.Select(alert => alert.Id));
    }

    [Fact]
    public void ResolveEmptyReason_ReturnsNoPersonalAccountsWhenListEmpty()
    {
        var hub = NotificationHub.CreateForTests();

        var reason = PersonalDashboardService.ResolveEmptyReason(
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            hub);

        Assert.Equal(PersonalDashboardEmptyReason.NoPersonalAccounts, reason);
    }

    [Fact]
    public void ResolveEmptyReason_ReturnsAllAccountsMutedWhenEveryAccountMuted()
    {
        var hub = NotificationHub.CreateForTests();
        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "Muted", NotificationsMuted = true }
        };
        var personalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "inst-1" };

        var reason = PersonalDashboardService.ResolveEmptyReason(instances, personalIds, hub);

        Assert.Equal(PersonalDashboardEmptyReason.AllAccountsMuted, reason);
    }

    [Fact]
    public void ResolveEmptyReason_ReturnsNoRecentActivityWhenAccountsExistWithoutAlerts()
    {
        var hub = NotificationHub.CreateForTests();
        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "Personal WhatsApp", Platform = "whatsapp" }
        };
        var personalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "inst-1" };

        var reason = PersonalDashboardService.ResolveEmptyReason(instances, personalIds, hub);

        Assert.Equal(PersonalDashboardEmptyReason.NoRecentActivity, reason);
    }

    [Fact]
    public void BuildSnapshot_SortsActivityByRecencyAndCapsDisplayCount()
    {
        var hub = NotificationHub.CreateForTests();
        var sessionManager = new InstanceSessionManager();
        var healthMonitor = AdapterHealthMonitor.Instance;
        var service = PersonalDashboardService.CreateForTests();
        var instanceId = $"inst-personal-{Guid.NewGuid():N}";

        for (var i = 0; i < PersonalDashboardService.MaxDisplayedActivityItems + 3; i++)
        {
            hub.AddAlert(new NotificationAlert
            {
                Id = $"alert-{i}",
                InstanceId = instanceId,
                InstanceDisplayName = "Personal",
                Platform = "whatsapp",
                Title = $"Alert {i}",
                ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }

        healthMonitor.MarkReady(instanceId, "whatsapp");

        try
        {
            var snapshot = service.BuildSnapshot(
                [new MessengerInstance { Id = instanceId, DisplayName = "Personal", Platform = "whatsapp" }],
                hub,
                sessionManager,
                ResourceMonitorService.CreateForTests(() => 1_048_576),
                healthMonitor);

            Assert.Equal(PersonalDashboardService.MaxDisplayedActivityItems, snapshot.RecentActivity.Count);
            Assert.Equal("Alert 0", snapshot.RecentActivity[0].Title);
            Assert.Equal(PersonalDashboardEmptyReason.HasData, snapshot.EmptyReason);
        }
        finally
        {
            healthMonitor.RemoveInstance(instanceId);
        }
    }

    [Fact]
    public void BuildSnapshot_IncludesConnectionStatusOnTiles()
    {
        var hub = NotificationHub.CreateForTests();
        var sessionManager = new InstanceSessionManager();
        var healthMonitor = AdapterHealthMonitor.Instance;
        var connectionService = InstanceConnectionStatusService.Instance;
        var instanceId = $"inst-connection-{Guid.NewGuid():N}";

        connectionService.SetLoggedOut(instanceId, "Session expired");
        healthMonitor.MarkReady(instanceId, "whatsapp");

        try
        {
            var snapshot = PersonalDashboardService.CreateForTests().BuildSnapshot(
                [new MessengerInstance { Id = instanceId, DisplayName = "Personal", Platform = "whatsapp" }],
                hub,
                sessionManager,
                ResourceMonitorService.CreateForTests(),
                healthMonitor,
                connectionService);

            var tile = Assert.Single(snapshot.InstanceTiles);
            Assert.Equal("Logged out", tile.ConnectionStatusLabel);
            Assert.Contains("Session expired", tile.DetailLine, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            connectionService.Remove(instanceId);
            healthMonitor.RemoveInstance(instanceId);
        }
    }

    [Fact]
    public void BuildSnapshot_IdentifiesMostUnreadInstance()
    {
        var hub = NotificationHub.CreateForTests();
        hub.UpdateBadgeCount("inst-a", 1);
        hub.UpdateBadgeCount("inst-b", 4);

        var sessionManager = new InstanceSessionManager();
        var healthMonitor = AdapterHealthMonitor.Instance;
        healthMonitor.MarkReady("inst-a", "slack");
        healthMonitor.MarkReady("inst-b", "whatsapp");

        try
        {
            var snapshot = PersonalDashboardService.CreateForTests().BuildSnapshot(
                [
                    new MessengerInstance { Id = "inst-a", DisplayName = "Alpha", Platform = "slack", SortOrder = 1 },
                    new MessengerInstance { Id = "inst-b", DisplayName = "Beta", Platform = "whatsapp", SortOrder = 2 }
                ],
                hub,
                sessionManager,
                ResourceMonitorService.CreateForTests(),
                healthMonitor);

            Assert.Equal("inst-b", snapshot.MostUnreadInstanceId);
            Assert.Equal(4, snapshot.MostUnreadCount);
            Assert.Equal(5, snapshot.TotalUnreadCount);
        }
        finally
        {
            healthMonitor.RemoveInstance("inst-a");
            healthMonitor.RemoveInstance("inst-b");
        }
    }
}
