using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class OperationalDataServiceTests
{
    [Fact]
    public async Task ClearAllAsync_RemovesAnalyticsAndThreadRegistry()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        MessageTriageService.Instance.ResetForTests([]);
        var originalThreads = ThreadRegistryService.Instance.GetAllThreads();
        var originalTriage = MessageTriageService.Instance.GetAllItems();

        try
        {
            MessageTriageService.Instance.RestoreItems([
                new MessageTriageItem
                {
                    Id = "item-1",
                    InstanceId = "inst-1",
                    InstanceDisplayName = "Branch",
                    Platform = "metabusiness",
                    MessagePreview = "Need help",
                    CustomerName = "Sara",
                    ConversationKey = "Sara",
                    UrgencyScore = 10,
                    Sentiment = MessageSentiment.Neutral,
                    TimestampUtc = DateTimeOffset.UtcNow
                }
            ]);
            ThreadRegistryService.Instance.RestoreThreads([
                new ThreadData
                {
                    ThreadId = "inst-1|Sara",
                    Platform = "metabusiness",
                    InstanceId = "inst-1",
                    BranchName = "F-11",
                    CustomerName = "Sara",
                    ConversationKey = "Sara"
                }
            ]);

            await OperationalDataService.ClearAllAsync();

            MessageTriageService.Instance.DrainPendingQueue();
            ThreadRegistryService.Instance.RestoreThreads([]);
            MessageTriageService.Instance.RestoreItems([]);

            Assert.Empty(ThreadRegistryService.Instance.GetAllThreads());
            Assert.Empty(MessageTriageService.Instance.GetAllItems());
        }
        finally
        {
            ThreadRegistryService.Instance.RestoreThreads(originalThreads);
            MessageTriageService.Instance.ResetForTests(originalTriage);
        }
    }

    [Fact]
    public async Task ClearAllAsync_ClearsNotificationBadgesAndAlerts()
    {
        var hub = NotificationHub.Instance;
        hub.UpdateBadgeCount("wave11-inst", 4);
        hub.AddAlert(NotificationAlert.Create("wave11-inst", "Work", "slack", "Ping"));

        await OperationalDataService.ClearAllAsync();

        Assert.Empty(hub.Alerts);
        Assert.Equal(0, hub.GetBadgeCount("wave11-inst"));
        Assert.Equal(0, hub.TotalUnreadCount);
    }
}
