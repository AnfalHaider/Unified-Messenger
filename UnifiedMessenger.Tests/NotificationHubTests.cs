using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class NotificationHubTests
{
    [Fact]
    public void AddAlert_SkipsMutedInstances()
    {
        var hub = NotificationHub.CreateForTests();
        hub.SyncMutedInstances(
        [
            new MessengerInstance
            {
                Id = "muted-1",
                DisplayName = "Muted",
                ProfileName = "muted",
                NotificationsMuted = true
            }
        ]);

        hub.AddAlert(NotificationAlert.Create("muted-1", "Muted", "slack", "Blocked"));

        Assert.Empty(hub.Alerts);
    }

    [Fact]
    public void UpdateBadgeCount_IgnoresPositiveCountsForMutedInstances()
    {
        var hub = NotificationHub.CreateForTests();
        hub.SyncMutedInstances(
        [
            new MessengerInstance
            {
                Id = "muted-1",
                DisplayName = "Muted",
                ProfileName = "muted",
                NotificationsMuted = true
            }
        ]);

        hub.UpdateBadgeCount("muted-1", 5);

        Assert.Equal(0, hub.GetBadgeCount("muted-1"));
        Assert.Equal(0, hub.TotalUnreadCount);
    }

    [Fact]
    public void UpdateBadgeCount_AllowsZeroingMutedInstanceBadge()
    {
        var hub = NotificationHub.CreateForTests();
        hub.UpdateBadgeCount("inst-1", 4);
        hub.SyncMutedInstances(
        [
            new MessengerInstance
            {
                Id = "inst-1",
                DisplayName = "Work",
                ProfileName = "work",
                NotificationsMuted = true
            }
        ]);

        hub.UpdateBadgeCount("inst-1", 0);

        Assert.Equal(0, hub.GetBadgeCount("inst-1"));
    }

    [Fact]
    public void UpdateBadgeCount_DoesNotRaiseChangedWhenUnchanged()
    {
        var hub = NotificationHub.CreateForTests();
        var changes = 0;
        hub.Changed += (_, _) => changes++;

        hub.UpdateBadgeCount("inst-1", 2);
        var afterFirst = changes;
        hub.UpdateBadgeCount("inst-1", 2);

        Assert.Equal(afterFirst, changes);
    }

    [Fact]
    public void AddAlert_DeduplicatesRecentMatchingAlerts()
    {
        var hub = NotificationHub.CreateForTests();
        var alert = NotificationAlert.Create("inst-1", "Work", "slack", "Ping", "Body");

        hub.AddAlert(alert);
        hub.AddAlert(NotificationAlert.Create("inst-1", "Work", "slack", "Ping", "Body"));

        Assert.Single(hub.Alerts);
    }

    [Fact]
    public void MarkAllAlertsRead_MarksVisibleUnreadAlerts()
    {
        var hub = NotificationHub.CreateForTests();
        hub.AddAlert(NotificationAlert.Create("a", "Visible A", "slack", "One"));
        hub.AddAlert(NotificationAlert.Create("b", "Visible B", "telegram", "Two"));

        hub.MarkAllAlertsRead();

        Assert.Equal(0, hub.UnreadAlertCount);
        Assert.All(hub.GetAlertsSortedByInstance(), alert => Assert.True(alert.IsRead));
    }

    [Fact]
    public void Alerts_ExcludeMutedInstancesAfterSync()
    {
        var hub = NotificationHub.CreateForTests();
        hub.AddAlert(NotificationAlert.Create("inst-1", "Work", "slack", "Ping"));
        hub.SyncMutedInstances(
        [
            new MessengerInstance
            {
                Id = "inst-1",
                DisplayName = "Work",
                ProfileName = "work",
                NotificationsMuted = true
            }
        ]);

        Assert.Empty(hub.Alerts);
        Assert.Equal(0, hub.UnreadAlertCount);
    }

    [Fact]
    public void AddAlert_TrimsToMaxAlerts()
    {
        var hub = NotificationHub.CreateForTests();

        for (var i = 0; i < NotificationHub.MaxAlerts + 5; i++)
        {
            hub.AddAlert(NotificationAlert.Create("inst-1", "Work", "slack", $"Alert {i}"));
        }

        Assert.Equal(NotificationHub.MaxAlerts, hub.Alerts.Count);
    }

    [Fact]
    public void SyncMutedInstances_RaisesChangedWhenMuteSetChanges()
    {
        var hub = NotificationHub.CreateForTests();
        var changes = 0;
        hub.Changed += (_, e) =>
        {
            if (e.Kind == NotificationChangeKind.BadgeUpdated)
            {
                changes++;
            }
        };

        hub.SyncMutedInstances(
        [
            new MessengerInstance { Id = "inst-1", DisplayName = "Work", ProfileName = "work", NotificationsMuted = true }
        ]);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void GetAlertsGroupedByInstance_GroupsByInstanceId()
    {
        var hub = NotificationHub.CreateForTests();
        hub.AddAlert(NotificationAlert.Create("a1", "Work", "slack", "Ping A"));
        hub.AddAlert(NotificationAlert.Create("a2", "Work", "telegram", "Ping B"));

        var groups = hub.GetAlertsGroupedByInstance();

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, group => group.InstanceId == "a1");
        Assert.Contains(groups, group => group.InstanceId == "a2");
    }
}
