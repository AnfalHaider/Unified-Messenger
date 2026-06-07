using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class MessageTriageServiceTests
{
    [Fact]
    public void ProcessInbound_BuildsUrgentQueueOrderedByScore()
    {
        var service = new MessageTriageService();
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "meta-1",
                DisplayName = "Depilex F-11",
                ProfileName = "meta-1",
                Platform = "metabusiness",
                StartUrl = "https://business.facebook.com",
                Category = WorkspaceCategory.Professional
            }
        };

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "meta-1",
                Platform = "metabusiness",
                MessageText = "Thanks for the great service yesterday!",
                CustomerName = "Ali"
            },
            "Depilex F-11");

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "meta-1",
                Platform = "metabusiness",
                MessageText = "I need to cancel my booking immediately, this is urgent.",
                CustomerName = "Sara"
            },
            "Depilex F-11");

        var snapshot = service.BuildSnapshot(instances);

        Assert.Single(snapshot.UrgentQueue);
        Assert.Equal("Critical", snapshot.UrgentQueue[0].UrgencyLabel);
        Assert.Equal(2, snapshot.PositiveCount + snapshot.NeutralCount + snapshot.NegativeCount);
        Assert.Equal(1, snapshot.PositiveCount);
    }

    [Fact]
    public void BuildSnapshot_FiltersByBranchInstance()
    {
        var service = new MessageTriageService();

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "branch-a",
                Platform = "metabusiness",
                MessageText = "I need to cancel my booking immediately, this is urgent."
            },
            "Branch A");

        service.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "branch-b",
                Platform = "metabusiness",
                MessageText = "Refund my payment now, terrible service."
            },
            "Branch B");

        var filtered = service.BuildSnapshot(
        [
            new MessengerInstance
            {
                Id = "branch-a",
                DisplayName = "Branch A",
                ProfileName = "branch-a",
                Platform = "metabusiness",
                StartUrl = "https://example.com",
                Category = WorkspaceCategory.Professional
            }
        ]);

        Assert.Single(filtered.UrgentQueue);
        Assert.Equal("branch-a", filtered.UrgentQueue[0].InstanceId);
    }
}
