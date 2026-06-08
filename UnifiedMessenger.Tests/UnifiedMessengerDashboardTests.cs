using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ThreadRegistryServiceTests
{
    [Fact]
    public void UpsertFromTriageItem_TracksUnresolvedThread()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var item = CreateItem("inst-1", "Sara", "Need bridal quote for Saturday");

        registry.UpsertFromTriageItem(item, "Sara", "DHA-2");

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.Equal("inst-1|Sara", thread.ThreadId);
        Assert.False(thread.IsReplied);
        Assert.Equal("DHA-2", thread.BranchName);
    }

    [Fact]
    public void MarkThreadResolved_SetsRepliedAndClearsLeakage()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var item = CreateItem("inst-1", "Sara", "Package price?");

        registry.UpsertFromTriageItem(
            item,
            "Sara",
            "F-11",
            aiIntentCategory: UnifiedMessengerIntentCategory.PriceInquiry,
            estimatedValue: 25000,
            isRevenueLeakageRisk: true);

        registry.MarkThreadResolved("inst-1", "Sara", "Sara", DateTimeOffset.UtcNow);

        var thread = Assert.Single(registry.GetAllThreads());
        Assert.True(thread.IsReplied);
        Assert.False(thread.IsRevenueLeakageRisk);
        Assert.True(thread.LatencyMinutes >= 0);
    }

    [Fact]
    public void EvaluateRevenueLeakage_FlagsCommercialThreadsAfterThirtyMinutes()
    {
        var thread = new ThreadData
        {
            ThreadId = "inst|sara",
            Platform = "whatsappbusiness",
            InstanceId = "inst",
            BranchName = "DHA-2",
            CustomerName = "Sara",
            AiIntentCategory = UnifiedMessengerIntentCategory.Booking,
            LatencyMinutes = 35,
            IsReplied = false
        };

        Assert.True(ThreadRegistryService.EvaluateRevenueLeakage(thread));
    }

    private static MessageTriageItem CreateItem(string instanceId, string customer, string preview) =>
        new()
        {
            Id = $"{instanceId}|test",
            InstanceId = instanceId,
            InstanceDisplayName = "Depilex DHA-2",
            Platform = "whatsappbusiness",
            MessagePreview = preview,
            CustomerName = customer,
            UrgencyScore = 40,
            Sentiment = MessageSentiment.Neutral,
            TimestampUtc = DateTimeOffset.UtcNow
        };
}

[Collection(UnifiedMessengerSerialCollection.Name)]
public class UnifiedMessengerDashboardServiceTests : IDisposable
{
    private readonly IReadOnlyList<ThreadData> _originalThreads;

    public UnifiedMessengerDashboardServiceTests()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
    }

    public void Dispose() => ThreadRegistryService.Instance.RestoreThreads(_originalThreads);

    [Fact]
    public void BuildSnapshot_GroupsBranchMetricsAndImmediateQueue()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateItem("dha", "Aisha", "Urgent complaint"),
            "Aisha",
            "DHA-2",
            aiIntentCategory: UnifiedMessengerIntentCategory.Complaint,
            clientSentiment: ClientSentimentLabel.Critical,
            operationalUrgency: 5,
            nextActionSummary: "Call client about service delay.");

        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "dha",
                DisplayName = "Depilex DHA-2",
                Platform = "whatsappbusiness",
                Category = WorkspaceCategory.Professional
            }
        };

        var snapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot(instances);

        Assert.Single(snapshot.BranchMetrics);
        Assert.Equal("DHA-2", snapshot.BranchMetrics[0].BranchName);
        Assert.Single(snapshot.ImmediateActionQueue);
        Assert.Equal("Call client about service delay.", snapshot.ImmediateActionQueue[0].NextActionSummary);
        Assert.Equal(1, snapshot.OpenThreadCount);
        Assert.Equal(0, snapshot.HangingLeadCount);
        Assert.Equal(1, snapshot.ImmediateActionCount);
        Assert.Equal(1, snapshot.ImmediateActionTotal);
        Assert.Equal(1, snapshot.ImmediateActionQueueCount);
    }

    [Fact]
    public void BuildSnapshot_ImmediateActionQueueCount_CappedAtDisplayLimit()
    {
        var instance = new MessengerInstance
        {
            Id = "dha",
            DisplayName = "Depilex DHA-2",
            Platform = "whatsappbusiness",
            Category = WorkspaceCategory.Professional
        };

        for (var index = 0; index < 30; index++)
        {
            ThreadRegistryService.Instance.UpsertFromTriageItem(
                CreateItem("dha", $"Customer {index}", $"Message {index}"),
                $"Customer {index}",
                "DHA-2",
                operationalUrgency: 5,
                nextActionSummary: $"Action {index}");
        }

        var snapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot([instance]);

        Assert.Equal(30, snapshot.ImmediateActionTotal);
        Assert.Equal(30, snapshot.ImmediateActionCount);
        Assert.Equal(UnifiedMessengerDashboardService.ImmediateActionQueueDisplayLimit, snapshot.ImmediateActionQueueCount);
        Assert.Equal(UnifiedMessengerDashboardService.ImmediateActionQueueDisplayLimit, snapshot.ImmediateActionQueue.Count);
    }

    [Fact]
    public void BuildSnapshot_CountsHangingLeads()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateItem("dha", "Nida", "Still thinking about quote", DateTimeOffset.UtcNow.AddMinutes(-45)),
            "Nida",
            "DHA-2",
            aiIntentCategory: UnifiedMessengerIntentCategory.PriceInquiry,
            estimatedValue: 45000,
            isRevenueLeakageRisk: true);

        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "dha",
                DisplayName = "Depilex DHA-2",
                Platform = "whatsappbusiness",
                Category = WorkspaceCategory.Professional
            }
        };

        var snapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot(instances);

        Assert.Equal(1, snapshot.HangingLeadCount);
        Assert.Equal(45000, snapshot.TotalRevenueAtRisk);
    }

    [Fact]
    public void BuildSnapshot_ExcludesSpamFromImmediateQueueAndBranchMetrics()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateItem("f11", "Vendor", "Custom foldable promo cards for brands."),
            "Vendor",
            "F-11",
            isSpamOrPromo: true,
            operationalUrgency: 1,
            nextActionSummary: "Promotional message — no action required");

        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "f11",
                DisplayName = "Depilex F-11",
                Platform = "whatsappbusiness",
                Category = WorkspaceCategory.Professional
            }
        };

        var snapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot(instances);

        Assert.Empty(snapshot.ImmediateActionQueue);
        Assert.Equal(0, snapshot.OpenThreadCount);
        Assert.Equal(0, snapshot.BranchMetrics[0].UnresolvedCount);
        Assert.Equal(0, snapshot.BranchMetrics[0].AverageLatencyMinutes);
    }

    [Fact]
    public void ResolveLatencyColor_UsesThresholdBands()
    {
        Assert.Equal("Green", UnifiedMessengerDashboardService.ResolveLatencyColor(10));
        Assert.Equal("Amber", UnifiedMessengerDashboardService.ResolveLatencyColor(20));
        Assert.Equal("Red", UnifiedMessengerDashboardService.ResolveLatencyColor(45));
    }

    private static MessageTriageItem CreateItem(
        string instanceId,
        string customer,
        string preview,
        DateTimeOffset? timestampUtc = null) =>
        new()
        {
            Id = $"{instanceId}|test",
            InstanceId = instanceId,
            InstanceDisplayName = "Depilex DHA-2",
            Platform = "metabusiness",
            MessagePreview = preview,
            CustomerName = customer,
            UrgencyScore = 90,
            Sentiment = MessageSentiment.Negative,
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow
        };
}
