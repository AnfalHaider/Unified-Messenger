using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class SharedModelTests
{
    [Fact]
    public void NotificationAlert_Create_NormalizesPlatformAndTitle()
    {
        var alert = NotificationAlert.Create(
            " inst-1 ",
            "Support",
            "WHATSAPP",
            "  New message ",
            " Hello ");

        Assert.Equal("inst-1", alert.InstanceId);
        Assert.Equal("whatsapp", alert.Platform);
        Assert.Equal("New message", alert.Title);
        Assert.Equal("Hello", alert.Body);
        Assert.False(string.IsNullOrWhiteSpace(alert.Id));
    }

    [Fact]
    public void GoogleReviewAlert_Normalize_ClampsRatingAndBuildsId()
    {
        var alert = new GoogleReviewAlert
        {
            InstanceId = "gbp-1",
            ReviewId = "rev-9",
            Rating = 99
        };

        alert.Normalize();

        Assert.Equal(5, alert.Rating);
        Assert.Equal("gbp-1:rev-9", alert.Id);
        Assert.Equal("Customer", alert.ReviewerName);
    }

    [Fact]
    public void RelativeTimeFormatter_UsesSharedRules()
    {
        var now = DateTimeOffset.Now;
        Assert.Equal("Just now", RelativeTimeFormatter.Format(now.AddSeconds(-10), now));
        Assert.Equal("5m ago", RelativeTimeFormatter.Format(now.AddMinutes(-5), now));
    }

    [Fact]
    public void NotificationHub_GroupsAlertsByInstanceId()
    {
        var hub = NotificationHub.CreateForTests();
        hub.AddAlert(NotificationAlert.Create("a1", "Work", "slack", "Ping A"));
        hub.AddAlert(NotificationAlert.Create("a2", "Work", "telegram", "Ping B"));

        var groups = hub.GetAlertsGroupedByInstance();

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, group => group.InstanceId == "a1");
        Assert.Contains(groups, group => group.InstanceId == "a2");
    }

    [Fact]
    public void AdapterHealthStatus_Normalize_ResetsInvalidState()
    {
        var status = new AdapterHealthStatus
        {
            State = (AdapterHealthState)999,
            AdapterId = "  slack-adapter  "
        };

        status.Normalize();

        Assert.Equal(AdapterHealthState.Unknown, status.State);
        Assert.Equal("slack-adapter", status.AdapterId);
    }

    [Fact]
    public void NotificationFeedItem_Header_TrimsTitleAndClampsUnread()
    {
        var item = NotificationFeedItem.Header("  Sales  ", -2);

        Assert.True(item.IsGroupHeader);
        Assert.Equal("Sales", item.GroupTitle);
        Assert.Equal(0, item.GroupUnreadCount);
    }
}
