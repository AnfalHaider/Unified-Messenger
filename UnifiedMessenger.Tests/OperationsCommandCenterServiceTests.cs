using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class OperationsCommandCenterServiceTests : IDisposable
{
    private readonly IReadOnlyList<ThreadData> _originalThreads;
    private readonly IReadOnlyList<MessageTriageItem> _originalTriage;

    public OperationsCommandCenterServiceTests()
    {
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
        _originalTriage = MessageTriageService.Instance.GetAllItems();
        ThreadRegistryService.Instance.RestoreThreads([]);
        MessageTriageService.Instance.RestoreItems([]);
    }

    public void Dispose()
    {
        ThreadRegistryService.Instance.RestoreThreads(_originalThreads);
        MessageTriageService.Instance.RestoreItems(_originalTriage);
    }

    [Fact]
    public void BuildSnapshot_composesStatusFromThreadAndAnalyticsMetrics()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateTriageItem("dha", "Aisha", "Need quote"),
            "Aisha",
            "DHA-2",
            operationalUrgency: 5,
            nextActionSummary: "Send bridal package quote.");

        var instances = CreateProfessionalInstances("dha", "Depilex DHA-2");

        var snapshot = OperationsCommandCenterService.Instance.BuildSnapshot(
            instances,
            triageService: MessageTriageService.Instance);

        Assert.Equal(1, snapshot.Status.OpenThreadCount);
        Assert.Equal(1, snapshot.Status.ImmediateActionCount);
        Assert.Single(snapshot.ThreadOperations.ImmediateActionQueue);
        Assert.Single(snapshot.AiInsightFeed);
        Assert.NotEmpty(snapshot.Status.PlatformHealth);
        Assert.NotNull(snapshot.AnalyticsTrends.Triage);
        Assert.NotEmpty(snapshot.InstanceHealthChips);
    }

    [Fact]
    public void BuildSnapshot_branchFilter_matchesThreadDashboardParity()
    {
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateTriageItem("f11", "Sara", "Facial booking"),
            "Sara",
            "F-11",
            nextActionSummary: "Confirm Saturday slot.");

        ThreadRegistryService.Instance.UpsertFromTriageItem(
            CreateTriageItem("d12", "Noor", "Cancel booking"),
            "Noor",
            "D-12",
            nextActionSummary: "Process cancellation.");

        var instances = new[]
        {
            CreateInstance("f11", "Depilex F-11"),
            CreateInstance("d12", "Depilex D-12")
        };

        var facadeSnapshot = OperationsCommandCenterService.Instance.BuildSnapshot(
            instances,
            branchInstanceId: "f11",
            triageService: MessageTriageService.Instance);

        var threadSnapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot(instances, "f11");

        Assert.Single(facadeSnapshot.FilteredInstances);
        Assert.Equal("f11", facadeSnapshot.FilteredInstances[0].Id);
        Assert.Equal(threadSnapshot.OpenThreadCount, facadeSnapshot.Status.OpenThreadCount);
        Assert.Equal(threadSnapshot.ImmediateActionCount, facadeSnapshot.Status.ImmediateActionCount);
        Assert.Contains("F-11", facadeSnapshot.ScopeLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSnapshot_scopesPlatformIntelligenceToFilteredBranch()
    {
        var instances = new[]
        {
            CreateInstance("google-f11", "Depilex F-11 Google", "googlebusiness"),
            CreateInstance("meta-d12", "Depilex D-12 Meta", "metabusiness")
        };

        var allBranches = OperationsCommandCenterService.Instance.BuildSnapshot(instances);
        Assert.True(allBranches.PlatformIntelligence.HasGoogleInstances);
        Assert.True(allBranches.PlatformIntelligence.HasMetaInstances);
        Assert.Equal(2, allBranches.PlatformIntelligence.GoogleInstanceIds.Count + allBranches.PlatformIntelligence.MetaInstanceIds.Count);

        var filtered = OperationsCommandCenterService.Instance.BuildSnapshot(instances, branchInstanceId: "google-f11");
        Assert.True(filtered.PlatformIntelligence.HasGoogleInstances);
        Assert.False(filtered.PlatformIntelligence.HasMetaInstances);
        Assert.Single(filtered.PlatformIntelligence.GoogleInstanceIds);
        Assert.Empty(filtered.PlatformIntelligence.MetaInstanceIds);
    }
    private static MessageTriageItem CreateTriageItem(string instanceId, string customer, string preview) =>
        new()
        {
            Id = $"{instanceId}|test",
            InstanceId = instanceId,
            InstanceDisplayName = "Branch",
            Platform = "whatsappbusiness",
            MessagePreview = preview,
            CustomerName = customer,
            UrgencyScore = 80,
            Sentiment = MessageSentiment.Negative,
            TimestampUtc = DateTimeOffset.UtcNow
        };

    private static MessengerInstance[] CreateProfessionalInstances(string id, string displayName) =>
        [CreateInstance(id, displayName)];

    private static MessengerInstance CreateInstance(string id, string displayName, string platform = "whatsappbusiness") =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Platform = platform,
            Category = WorkspaceCategory.Professional
        };
}

public class OperationsCommandCenterInsightFeedBuilderTests
{
    [Fact]
    public void Build_prefersThreadSummary_overDuplicateExecutiveInsight()
    {
        var threadOperations = new UnifiedMessengerDashboardSnapshot
        {
            ImmediateActionQueue =
            [
                new ThreadData
                {
                    ThreadId = "dha|sara",
                    Platform = "whatsappbusiness",
                    InstanceId = "dha",
                    InstanceDisplayName = "Depilex DHA-2",
                    BranchName = "DHA-2",
                    CustomerName = "Sara",
                    NextActionSummary = "Call Sara about service delay.",
                    UrgencyScore = 5
                }
            ],
            AllThreads =
            [
                new ThreadData
                {
                    ThreadId = "dha|sara",
                    Platform = "whatsappbusiness",
                    InstanceId = "dha",
                    InstanceDisplayName = "Depilex DHA-2",
                    BranchName = "DHA-2",
                    CustomerName = "Sara",
                    NextActionSummary = "Call Sara about service delay.",
                    UrgencyScore = 5
                }
            ]
        };

        var triageItems = new[]
        {
            new MessageTriageItem
            {
                Id = "dha|msg-1",
                InstanceId = "dha",
                InstanceDisplayName = "Depilex DHA-2",
                Platform = "whatsappbusiness",
                MessagePreview = "Still waiting",
                CustomerName = "Sara",
                UrgencyScore = 90,
                Sentiment = MessageSentiment.Negative,
                InferenceSource = TriageInferenceSource.LocalAi,
                CoreSummary = "Escalated complaint requiring manager callback.",
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

        var feed = OperationsCommandCenterInsightFeedBuilder.Build(threadOperations, triageItems);

        Assert.Single(feed);
        Assert.Equal(OperationsInsightFeedKind.ThreadAction, feed[0].Kind);
        Assert.Equal("Call Sara about service delay.", feed[0].Summary);
    }

    [Fact]
    public void Build_includesExecutiveInsightWhenThreadHasNoSummary()
    {
        var threadOperations = UnifiedMessengerDashboardSnapshot.Empty;

        var triageItems = new[]
        {
            new MessageTriageItem
            {
                Id = "f11|msg-1",
                InstanceId = "f11",
                InstanceDisplayName = "Depilex F-11",
                Platform = "googlebusiness",
                MessagePreview = "Need appointment Friday",
                CustomerName = "Sara",
                UrgencyScore = 55,
                Sentiment = MessageSentiment.Neutral,
                InferenceSource = TriageInferenceSource.LocalAi,
                CoreSummary = "Confirm Friday appointment slot.",
                ExtractedEntities = new RichTriageExtractedEntities
                {
                    CustomerName = "Sara",
                    RequestedDate = "Friday"
                },
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

        var feed = OperationsCommandCenterInsightFeedBuilder.Build(threadOperations, triageItems);

        Assert.Single(feed);
        Assert.Equal(OperationsInsightFeedKind.ExecutiveInsight, feed[0].Kind);
        Assert.Equal("Confirm Friday appointment slot.", feed[0].Summary);
        Assert.Equal("Local AI", feed[0].SourceLabel);
    }

    [Fact]
    public void BuildConversationKey_isCaseInsensitive()
    {
        var left = OperationsCommandCenterInsightFeedBuilder.BuildConversationKey("Inst-A", "Sara");
        var right = OperationsCommandCenterInsightFeedBuilder.BuildConversationKey("inst-a", "sara");
        Assert.Equal(left, right, StringComparer.OrdinalIgnoreCase);
    }
}
