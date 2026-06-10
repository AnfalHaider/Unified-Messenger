using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class NotificationFeedPanelHelperTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(120, 99)]
    public void ResolveHeaderBadgeValue_ClampsUnreadCount(int unreadCount, int expected)
    {
        Assert.Equal(expected, NotificationFeedPanelHelper.ResolveHeaderBadgeValue(unreadCount));
    }

    [Theory]
    [InlineData(0, 0, false, false)]
    [InlineData(3, 0, true, false)]
    [InlineData(3, 2, true, true)]
    public void ResolveCommandStates_ReflectsAlertCounts(
        int totalAlerts,
        int unreadAlerts,
        bool clearEnabled,
        bool markAllReadEnabled)
    {
        var states = NotificationFeedPanelHelper.ResolveCommandStates(totalAlerts, unreadAlerts);

        Assert.Equal(clearEnabled, states.ClearEnabled);
        Assert.Equal(markAllReadEnabled, states.MarkAllReadEnabled);
    }

    [Fact]
    public void BuildFeedItemsStructure_UsesGroupHeaderBeforeAlerts()
    {
        var alert = NotificationAlert.Create(
            "inst-1",
            "Sales",
            "whatsapp",
            "New message",
            "Hello there",
            id: "alert-1");

        var group = new NotificationAlertGroup("inst-1", "Sales", [alert]);
        var header = NotificationFeedItem.Header(group.Key, NotificationFeedPanelHelper.CountUnreadAlerts(group));

        Assert.True(header.IsGroupHeader);
        Assert.Equal("Sales", header.GroupTitle);
        Assert.Equal(1, header.GroupUnreadCount);
    }

    [Fact]
    public void BuildInstanceLookup_IgnoresDuplicateAndBlankIds()
    {
        var lookup = NotificationFeedPanelHelper.BuildInstanceLookup([
            new MessengerInstance { Id = "inst-1", DisplayName = "One" },
            new MessengerInstance { Id = "inst-1", DisplayName = "Duplicate" },
            new MessengerInstance { Id = "   ", DisplayName = "Invalid" }
        ]);

        Assert.Single(lookup);
        Assert.Equal("One", lookup["inst-1"].DisplayName);
    }

    [Theory]
    [InlineData(true, 0.72)]
    [InlineData(false, 1)]
    public void ResolveCardOpacity_DistinguishesReadState(bool isRead, double expected)
    {
        Assert.Equal(expected, NotificationFeedPanelHelper.ResolveCardOpacity(isRead));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("alert-1", true)]
    public void IsValidAlertId_ValidatesDismissTarget(string? alertId, bool expected)
    {
        Assert.Equal(expected, NotificationFeedPanelHelper.IsValidAlertId(alertId));
    }

    [Fact]
    public void GroupAlertsByPlatform_GroupsAcrossInstances()
    {
        var alerts = new[]
        {
            NotificationAlert.Create("inst-1", "Sales WA", "whatsapp", "Msg 1", id: "a1"),
            NotificationAlert.Create("inst-2", "Support WA", "whatsapp", "Msg 2", id: "a2"),
            NotificationAlert.Create("inst-3", "Team TG", "telegram", "Msg 3", id: "a3")
        };

        var groups = NotificationFeedPanelHelper.GroupAlertsByPlatform(alerts, new Dictionary<string, MessengerInstance>());

        Assert.Equal(2, groups.Count);
        Assert.Equal("WhatsApp", groups[0].Key);
        Assert.Equal(2, groups[0].Count);
        Assert.Equal("Telegram", groups[1].Key);
        Assert.Single(groups[1]);
    }

    [Fact]
    public void GroupAlertsByPlatform_UsesInstancePlatformWhenAlertPlatformMissing()
    {
        var alert = NotificationAlert.Create("inst-1", "Sales", "not-a-platform", "Msg", id: "a1");
        var lookup = NotificationFeedPanelHelper.BuildInstanceLookup([
            new MessengerInstance { Id = "inst-1", DisplayName = "Sales", Platform = "messenger" }
        ]);

        var groups = NotificationFeedPanelHelper.GroupAlertsByPlatform([alert], lookup);

        Assert.Single(groups);
        Assert.Equal("Messenger", groups[0].Key);
    }
}
