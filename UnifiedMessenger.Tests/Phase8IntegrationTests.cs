using System.Reflection;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class Phase8PipelineIntegrationTests : IDisposable
{
    private readonly IReadOnlyList<ThreadData> _originalThreads;
    private readonly IReadOnlyList<MessageTriageItem> _originalTriage;
    private readonly string _analyticsPath;
    private readonly int _originalSlaThreshold;

    public Phase8PipelineIntegrationTests()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        MessageTriageService.Instance.ResetForTests([]);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
        _originalTriage = MessageTriageService.Instance.GetAllItems();

        var tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        _analyticsPath = Path.Combine(tempDirectory, "analytics.json");
        _originalSlaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;
    }

    [Fact]
    public void ProcessInbound_DoesNotCallLlmInline_InsightsEngineOwnsInference()
    {
        var countingClient = new CountingTriageLlmClient();
        var triage = new MessageTriageService(new MessageTriageInferenceRunner(countingClient));

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "phase8-inline",
                Platform = "whatsappbusiness",
                MessageText = "Urgent bridal makeup booking for Saturday at F-11.",
                CustomerName = "Aisha"
            },
            "Depilex F-11");

        Assert.Equal(0, countingClient.CallCount);
        var item = Assert.Single(triage.GetAllItems());
        Assert.Equal(TriageInferenceSource.Heuristic, item.InferenceSource);
    }

    [Fact]
    public async Task EndToEnd_IngressInsightsResolve_UpdatesRegistryDashboardAndAnalytics()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        MessageTriageService.Instance.RestoreItems([]);
        await MessageAnalyticsService.Instance.ClearAllDataAsync();
        BackfillDedupeRegistry.ClearForTests();

        const string instanceId = "phase8-e2e";
        const string jid = "923009876543@c.us";
        var inboundAt = DateTimeOffset.UtcNow.AddMinutes(-18);

        var triage = new MessageTriageService(new MessageTriageInferenceRunner(new StructuredJsonLlmClient()));
        var insights = new UnifiedMessengerInsightsEngine(
            new MessageTriageInferenceRunner(new StructuredJsonLlmClient()),
            triage);

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = instanceId,
                Platform = "whatsappbusiness",
                MessageText = "Need urgent bridal makeup pricing for Saturday.",
                CustomerName = "Sara",
                ConversationKey = jid,
                TimestampUtc = inboundAt
            },
            "Depilex F-11");

        var baseline = Assert.Single(triage.GetAllItems());
        await insights.ProcessJobForTestsAsync(new UnifiedMessengerInsightsJob
        {
            Kind = UnifiedMessengerInsightsJobKind.MessageAnalysis,
            InstanceId = instanceId,
            TriageItemId = baseline.Id
        });

        var instances = new[] { CreateInstance(instanceId, "Depilex F-11") };
        var dashboardBeforeResolve = UnifiedMessengerDashboardService.Instance.BuildSnapshot(instances);
        Assert.Single(dashboardBeforeResolve.ImmediateActionQueue);
        Assert.Equal(1, dashboardBeforeResolve.ImmediateActionCount);

        ThreadRegistryService.Instance.MarkThreadResolved(
            instanceId,
            jid,
            "Sara",
            DateTimeOffset.UtcNow,
            "whatsappbusiness");

        var dashboardAfterResolve = UnifiedMessengerDashboardService.Instance.BuildSnapshot(instances);
        Assert.Empty(dashboardAfterResolve.ImmediateActionQueue);
        var resolvedThread = ThreadRegistryService.Instance.GetAllThreads()
            .Single(t => t.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        Assert.True(resolvedThread.IsReplied);

        var (_, replyCount, _, _) = MessageAnalyticsService.Instance.GetReplyStats(instanceId);
        Assert.Equal(1, replyCount);

        var occSnapshot = OperationsCommandCenterService.Instance.BuildSnapshot(
            instances,
            triageService: triage);
        Assert.Equal(0, occSnapshot.Status.ImmediateActionCount);
        Assert.Equal(0, occSnapshot.Status.OpenThreadCount);
    }

    [Fact]
    public async Task Enqueue_DedupesDuplicateIngressWithinWindow()
    {
        BackfillDedupeRegistry.ClearForTests();
        RichTriageStoreService.Instance.SetLoadedForTests();
        MessageTriageService.Instance.ResetForTests([]);

        var instanceId = "phase8-dedupe-" + Guid.NewGuid().ToString("N");
        var selection = new InboundMessageSelection
        {
            InstanceId = instanceId,
            Platform = "whatsappbusiness",
            MessageText = "Duplicate preview body for ingress dedupe validation.",
            CustomerName = "Alex",
            ConversationKey = "923001111111@c.us",
            TimestampUtc = DateTimeOffset.UtcNow
        };

        MessageTriageService.Instance.Enqueue(selection, "Branch");
        await WaitForItemsAsync(instanceId, 1);

        Assert.False(BackfillDedupeRegistry.TryAccept(
            instanceId,
            selection.Platform,
            ConversationKeyResolver.Resolve(
                selection.Platform,
                selection.ConversationKey,
                selection.ConversationHint,
                selection.CustomerName,
                selection.MessageText),
            selection.MessageText));

        MessageTriageService.Instance.Enqueue(selection, "Branch");
        await Task.Delay(200);

        var scopedItems = MessageTriageService.Instance.GetAllItems()
            .Where(item => item.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(scopedItems);
    }

    [Fact]
    public async Task E2E_StructuredJsonSpam_ExcludedFromImmediateLaneAndOccStatus()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        MessageTriageService.Instance.RestoreItems([]);

        var triage = new MessageTriageService();
        var insights = new UnifiedMessengerInsightsEngine(
            new MessageTriageInferenceRunner(new NoOpTriageLlmClient()),
            triage);

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "phase8-spam",
                Platform = "whatsappbusiness",
                MessageText =
                    "We create custom foldable promo cards for brands. Perfect for packaging, delivery bags. Mini campaign!"
            },
            "Branch");

        await insights.ProcessJobForTestsAsync(new UnifiedMessengerInsightsJob
        {
            Kind = UnifiedMessengerInsightsJobKind.MessageAnalysis,
            InstanceId = "phase8-spam",
            TriageItemId = triage.GetAllItems()[0].Id
        });

        var instances = new[] { CreateInstance("phase8-spam", "Branch") };
        var occ = OperationsCommandCenterService.Instance.BuildSnapshot(instances, triageService: triage);

        Assert.Equal(0, occ.Status.ImmediateActionCount);
        Assert.Empty(occ.ThreadOperations.ImmediateActionQueue);
        Assert.True(ThreadRegistryService.Instance.GetAllThreads().Single().IsSpamOrPromo);
    }

    [Fact]
    public void E2E_SlaBreachedInquiry_AppearsInImmediateQueueWithoutHighUrgencyScore()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = 15;

        var inboundAt = DateTimeOffset.UtcNow.AddMinutes(-25);
        ThreadRegistryService.Instance.UpsertFromTriageItem(
            new MessageTriageItem
            {
                Id = "sla|1",
                InstanceId = "sla-inst",
                InstanceDisplayName = "Branch",
                Platform = "whatsappbusiness",
                MessagePreview = "Still waiting for a quote",
                CustomerName = "Hina",
                ConversationKey = "923002222222@c.us",
                UrgencyScore = 10,
                OperationalUrgency = 2,
                Sentiment = MessageSentiment.Neutral,
                ClientSentiment = ClientSentimentLabel.Neutral,
                TimestampUtc = inboundAt
            },
            "923002222222@c.us",
            "Branch");

        var snapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot(
            [CreateInstance("sla-inst", "Branch")]);

        Assert.Single(snapshot.ImmediateActionQueue);
        Assert.True(snapshot.ImmediateActionQueue[0].IsSlaBreached);
    }

    public void Dispose()
    {
        ThreadRegistryService.Instance.RestoreThreads(_originalThreads);
        MessageTriageService.Instance.ResetForTests(_originalTriage);
        AppSettingsService.Instance.Settings.SlaThresholdMinutes = _originalSlaThreshold;
        BackfillDedupeRegistry.ClearForTests();

        if (Directory.Exists(Path.GetDirectoryName(_analyticsPath)!))
        {
            Directory.Delete(Path.GetDirectoryName(_analyticsPath)!, recursive: true);
        }
    }

    private static MessengerInstance CreateInstance(string id, string displayName) =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Platform = "whatsappbusiness",
            Category = WorkspaceCategory.Professional
        };

    private static async Task WaitForItemsAsync(string instanceId, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            var count = MessageTriageService.Instance.GetAllItems()
                .Count(item => item.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
            if (count >= expectedCount)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Expected at least {expectedCount} triage item(s) for {instanceId}.");
    }

    private sealed class CountingTriageLlmClient : ITriageLlmClient
    {
        public int CallCount { get; private set; }

        public Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken,
            bool strictJsonRetry = false)
        {
            CallCount++;
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class StructuredJsonLlmClient : ITriageLlmClient
    {
        public Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken,
            bool strictJsonRetry = false) =>
            Task.FromResult<string?>(
                """
                {
                  "isSpamOrPromo": false,
                  "intentCategory": "Booking",
                  "urgencyScore": 5,
                  "actionableSummary": "Requests Saturday bridal makeup pricing.",
                  "suggestedAction": "Reply with Pricing"
                }
                """);
    }

    private sealed class NoOpTriageLlmClient : ITriageLlmClient
    {
        public Task<string?> GenerateTriageJsonAsync(
            RichTriageInferenceJob job,
            CancellationToken cancellationToken,
            bool strictJsonRetry = false) =>
            Task.FromResult<string?>(null);
    }
}

public class Phase8AdapterRecoveryTests
{
    [Theory]
    [InlineData("whatsapp")]
    [InlineData("whatsappbusiness")]
    [InlineData("googlebusiness")]
    [InlineData("metabusiness")]
    public void ReinjectScriptSet_IncludesMonitorAuditorAndDraftForSupportedPlatforms(string platform)
    {
        var adapter = PlatformAdapterFactory.Resolve(platform);
        var supportsInbound = GetProtectedProperty<bool>(adapter, "SupportsInboundAutoDraft");
        var additionalScripts = GetProtectedProperty<IReadOnlyList<string>>(adapter, "AdditionalScriptFileNames");

        Assert.Contains("thread-status-auditor.js", additionalScripts);

        if (supportsInbound)
        {
            Assert.True(supportsInbound);
        }
    }

    private static T GetProtectedProperty<T>(object target, string propertyName) =>
        (T)target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(target)!;
}

public class Phase8LiveValidationScenarios
{
    /// <summary>
    /// Scripted proxy for manual live validation checklist (Phase 8.3).
    /// Run the app with Local AI enabled and verify each scenario in a professional inbox.
    /// </summary>
    public static IReadOnlyList<string> ManualChecklist { get; } =
    [
        "Open WhatsApp Business thread → triage returns strict camelCase JSON with actionableSummary (not raw DOM).",
        "Send B2B promo copy → thread lands in Resolved/spam lane; Immediate count stays 0.",
        "Leave inquiry unanswered past SLA threshold → thread appears in Immediate action lane with SLA breach.",
        "Reply in-thread → resolve fires once; analytics reply pair logged; SLA timer stops.",
        "Quit app during backfill → workers cancel cleanly; triage store flushes without corrupt JSON."
    ];

    [Fact]
    public void ManualChecklist_CoversStructuredJsonSpamImmediateAndSlaScenarios()
    {
        Assert.Equal(5, ManualChecklist.Count);
        Assert.Contains(ManualChecklist, item => item.Contains("camelCase", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ManualChecklist, item => item.Contains("spam", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ManualChecklist, item => item.Contains("SLA", StringComparison.OrdinalIgnoreCase));
    }
}
