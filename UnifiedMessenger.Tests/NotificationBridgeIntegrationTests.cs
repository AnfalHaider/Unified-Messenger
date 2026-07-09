using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

public class NotificationBridgeIntegrationTests
{
    [Fact]
    public void BadgeCountPayload_ThroughAdapterPath_UpdatesHubState()
    {
        var hub = NotificationHub.CreateForTests();
        var instance = new MessengerInstance
        {
            Id = "slack-work",
            DisplayName = "Slack Work",
            Platform = "slack",
            ProfileName = "slack-work"
        };

        var payload = """
            {
              "type": "badge-count",
              "instanceId": "slack-work",
              "count": 7
            }
            """;

        using var document = WebMessageParser.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("badge-count", root.GetProperty("type").GetString());
        Assert.True(WebMessageParser.MatchesInstance(root, instance));
        Assert.Equal(7, WebMessageParser.ReadNonNegativeInt(root, "count"));

        var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
        adapter.HandleWebMessage(payload, hub, instance);

        Assert.Equal(7, hub.GetBadgeCount("slack-work"));
        Assert.Equal(7, hub.TotalUnreadCount);
    }

    [Fact]
    public void BadgeCountPayload_DirectHubUpdate_ChangesBadgeAndTotal()
    {
        var hub = NotificationHub.CreateForTests();

        var payload = """
            {
              "type": "badge-count",
              "instanceId": "wa-personal",
              "count": 3
            }
            """;

        using var document = WebMessageParser.Parse(payload);
        var root = document.RootElement;
        var count = WebMessageParser.ReadNonNegativeInt(root, "count");
        var instanceId = root.GetProperty("instanceId").GetString()!;

        hub.UpdateBadgeCount(instanceId, count);

        Assert.Equal(3, hub.GetBadgeCount("wa-personal"));
        Assert.Equal(3, hub.TotalUnreadCount);

        hub.UpdateBadgeCount(instanceId, 0);

        Assert.Equal(0, hub.GetBadgeCount("wa-personal"));
        Assert.Equal(0, hub.TotalUnreadCount);
    }

    [Fact]
    public void BadgeCountPayload_MismatchedInstanceId_DoesNotUpdateHub()
    {
        var hub = NotificationHub.CreateForTests();
        var instance = new MessengerInstance
        {
            Id = "teams-work",
            DisplayName = "Teams Work",
            Platform = "teams",
            ProfileName = "teams-work"
        };

        var payload = """
            {
              "type": "badge-count",
              "instanceId": "other-instance",
              "count": 5
            }
            """;

        var adapter = PlatformAdapterFactory.Resolve(instance.Platform);
        adapter.HandleWebMessage(payload, hub, instance);

        Assert.Equal(0, hub.GetBadgeCount("teams-work"));
        Assert.Equal(0, hub.TotalUnreadCount);
    }
}
