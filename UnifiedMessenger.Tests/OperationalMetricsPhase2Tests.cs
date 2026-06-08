using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OperationalMetricsPhase2Tests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;
    private readonly int _originalSlaThreshold;

    public OperationalMetricsPhase2Tests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "analytics.json");
        _originalSlaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
    }

    [Fact]
    public void ThreadData_IsSlaBreached_UsesSettingsThreshold()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 20;

        var thread = new ThreadData
        {
            ThreadId = "inst|chat",
            Platform = "whatsappbusiness",
            InstanceId = "inst",
            LatencyMinutes = 21
        };

        Assert.True(thread.IsSlaBreached);

        thread.LatencyMinutes = 19;
        Assert.False(thread.IsSlaBreached);
    }

    [Fact]
    public void ThreadData_IsImmediateAction_IncludesSlaBreachedInquiry()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;

        var thread = new ThreadData
        {
            ThreadId = "inst|chat",
            Platform = "whatsappbusiness",
            InstanceId = "inst",
            LatencyMinutes = 30,
            UrgencyScore = 1,
            ClientSentiment = ClientSentimentLabel.Neutral
        };

        Assert.True(thread.IsSlaBreached);
        Assert.True(thread.IsImmediateAction);
    }

    [Fact]
    public void RecordThreadReply_PairsLatencyFromFirstInbound()
    {
        var service = new MessageAnalyticsService(_storePath);
        var inboundAt = DateTimeOffset.UtcNow.AddMinutes(-18);
        var resolvedAt = DateTimeOffset.UtcNow;

        service.RecordThreadReply("inst-1", "923001234567@c.us", inboundAt, resolvedAt, "Sara");

        var (_, replyCount, _, _) = service.GetReplyStats("inst-1");
        Assert.Equal(1, replyCount);
        Assert.InRange(service.GetHistoricalSlaBreachCount("inst-1"), 1, 1);
    }

    [Fact]
    public void RecordThreadReply_DedupesDuplicateResolveWithinWindow()
    {
        var service = new MessageAnalyticsService(_storePath);
        var inboundAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var resolvedAt = DateTimeOffset.UtcNow;

        service.RecordThreadReply("inst-1", "923001234567@c.us", inboundAt, resolvedAt, "Sara");
        service.RecordThreadReply("inst-1", "923001234567@c.us", inboundAt, resolvedAt, "Sara");

        var (_, replyCount, _, _) = service.GetReplyStats("inst-1");
        Assert.Equal(1, replyCount);
    }

    [Fact]
    public async Task MarkThreadResolved_RecordsAnalyticsReplyPair()
    {
        var registry = ThreadRegistryService.CreateForTests();
        var analytics = MessageAnalyticsService.Instance;
        await analytics.ClearAllDataAsync();
        var instanceId = "inst-reply-" + Guid.NewGuid().ToString("N");
        var inboundAt = DateTimeOffset.UtcNow.AddMinutes(-22);

        registry.UpsertFromTriageItem(
            new MessageTriageItem
            {
                Id = instanceId + "|triage",
                InstanceId = instanceId,
                InstanceDisplayName = "Branch",
                Platform = "whatsappbusiness",
                MessagePreview = "Need help",
                CustomerName = "Ali",
                ConversationKey = "923001234567@c.us",
                UrgencyScore = 20,
                Sentiment = MessageSentiment.Neutral,
                TimestampUtc = inboundAt
            },
            "923001234567@c.us",
            "Branch");

        registry.MarkThreadResolved(
            instanceId,
            "923001234567@c.us",
            "Ali",
            DateTimeOffset.UtcNow,
            "whatsappbusiness");

        var (_, replyCount, _, _) = analytics.GetReplyStats(instanceId);
        Assert.Equal(1, replyCount);
    }

    [Fact]
    public void OperationalMetricsHelper_CountActiveSlaBreaches_ExcludesSpamAndResolved()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;

        var threads = new[]
        {
            new ThreadData
            {
                ThreadId = "a",
                Platform = "whatsapp",
                InstanceId = "i",
                LatencyMinutes = 30,
                IsSpamOrPromo = true
            },
            new ThreadData
            {
                ThreadId = "b",
                Platform = "whatsapp",
                InstanceId = "i",
                LatencyMinutes = 30,
                IsReplied = true
            },
            new ThreadData
            {
                ThreadId = "c",
                Platform = "whatsapp",
                InstanceId = "i",
                LatencyMinutes = 30
            }
        };

        Assert.Equal(1, OperationalMetricsHelper.CountActiveSlaBreaches(threads));
    }

    [Fact]
    public void OperationalMetricsHelper_BuildHighlights_IncludesConversationKeyForWaitingThreads()
    {
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

        var threads = new[]
        {
            new ThreadData
            {
                ThreadId = "dha|sara",
                Platform = "whatsappbusiness",
                InstanceId = "dha",
                BranchName = "DHA-2",
                CustomerName = "Sara",
                ConversationKey = "sara@lid",
                LatencyMinutes = 12
            }
        };

        var highlights = OperationalMetricsHelper.BuildHighlights(instances, threads, []);

        var highlight = Assert.Single(highlights);
        Assert.Equal("Sara", highlight.Title);
        Assert.Equal("sara@lid", highlight.ConversationKey);
        Assert.Equal("dha", highlight.InstanceId);
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = _originalSlaThreshold;

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
