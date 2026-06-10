using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests.Backfill;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class WhatsAppBackfillProviderTests
{
    [Theory]
    [InlineData("whatsapp", true)]
    [InlineData("whatsappbusiness", true)]
    [InlineData("WhatsAppBusiness", true)]
    [InlineData("metabusiness", false)]
    [InlineData("googlebusiness", false)]
    public void CanBackfill_AcceptsWhatsAppPlatforms(string platform, bool expected)
    {
        var provider = new WhatsAppBackfillProvider();
        var instance = new MessengerInstance
        {
            Id = "inst",
            DisplayName = "Branch",
            Platform = platform,
            Category = WorkspaceCategory.Professional
        };

        Assert.Equal(expected, provider.CanBackfill(instance));
    }
}

[Collection(UnifiedMessengerSerialCollection.Name)]
public class BackfillPhase5Tests : IDisposable
{
    public BackfillPhase5Tests()
    {
        BackfillSyncManager.Instance.ResetStateForTests();
        BackfillSyncManager.TestProviderOverride = null;

        var settings = AppSettingsService.Instance.Settings;
        settings.EnableStartupBackfill = true;
        PlatformModuleSettingsHelper.SetPlatformEnabled(settings, "whatsapp", true);
        PlatformModuleSettingsHelper.SetPlatformEnabled(settings, "whatsappbusiness", true);
        PlatformModuleSettingsHelper.SetPlatformEnabled(settings, "metabusiness", true);
        PlatformModuleSettingsHelper.SetPlatformEnabled(settings, "googlebusiness", true);
    }

    [Fact]
    public void Schedule_WhatsAppBusiness_IsNotSkippedWhenEnabled()
    {
        AppSettingsService.Instance.Settings.EnableStartupBackfill = true;

        var instance = new MessengerInstance
        {
            Id = "wab-1",
            DisplayName = "WAB Branch",
            Platform = "whatsappbusiness",
            Category = WorkspaceCategory.Professional
        };

        BackfillSyncManager.Instance.Schedule(instance);

        Assert.NotEqual(BackfillSyncState.Skipped, BackfillSyncManager.Instance.GetState(instance.Id));
    }

    [Fact]
    public async Task RunBackfillForTestsAsync_StoresLastResultAndRaisesProgress()
    {
        BackfillSyncManager.TestProviderOverride = new CountingBackfillProvider();
        var instance = new MessengerInstance
        {
            Id = "ok-2",
            DisplayName = "Branch",
            Platform = "testcount",
            Category = WorkspaceCategory.Professional
        };

        BackfillProgressEventArgs? progress = null;
        BackfillSyncManager.Instance.ProgressChanged += (_, args) => progress = args;

        var result = await BackfillSyncManager.Instance.RunBackfillForTestsAsync(instance);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.TriageEnqueued);
        Assert.Equal(3, BackfillSyncManager.Instance.GetLastResult(instance.Id)?.TriageEnqueued);
        Assert.NotNull(progress);
        Assert.Equal(instance.Id, progress!.InstanceId);
        Assert.Equal(BackfillSyncState.Completed, BackfillSyncManager.Instance.GetState(instance.Id));
        Assert.Equal(3, progress.Result?.TriageEnqueued);
    }

    [Theory]
    [InlineData("metabusiness", true)]
    [InlineData("googlebusiness", true)]
    [InlineData("whatsapp", false)]
    public void MetaGoogleBackfillProvider_CanBackfillDashboardPlatforms(string platform, bool expected)
    {
        var provider = new MetaGoogleBackfillProvider();
        var instance = new MessengerInstance
        {
            Id = "inst",
            DisplayName = "Branch",
            Platform = platform,
            Category = WorkspaceCategory.Professional
        };

        Assert.Equal(expected, provider.CanBackfill(instance));
    }

    [Fact]
    public void BuildBackfillSummary_ShowsScrapeOnlyReason()
    {
        var summary = DashboardDataHealthHelper.BuildBackfillSummary(
            BackfillSyncState.Completed,
            new BackfillResult
            {
                IsScrapeOnly = true,
                ScrapeOnlyReason = MetaGoogleBackfillProvider.ScrapeOnlyReasonMessage
            });

        Assert.Contains("scrape", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildBackfillSummary_IncludesCountsForCompletedRun()
    {
        var summary = DashboardDataHealthHelper.BuildBackfillSummary(
            BackfillSyncState.Completed,
            new BackfillResult
            {
                TriageEnqueued = 5,
                AnalyticsInboundRecorded = 5,
                TriageSkippedDuplicate = 2
            });

        Assert.Contains("5 triage", summary, StringComparison.Ordinal);
        Assert.Contains("2 skipped", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TryAccept_UsesCanonicalJidKeyAcrossBackfillAndLivePaths()
    {
        BackfillDedupeRegistry.ClearForTests();

        const string jid = "923001234567@c.us";
        const string body = "Need bridal makeup pricing for Saturday";

        Assert.True(BackfillDedupeRegistry.TryAccept("inst-1", "whatsappbusiness", jid, body));

        var resolvedKey = ConversationKeyResolver.Resolve(
            "whatsappbusiness",
            jid,
            jid,
            "Sara",
            body);

        Assert.Equal(jid, resolvedKey);
        Assert.False(BackfillDedupeRegistry.TryAccept("inst-1", "whatsappbusiness", resolvedKey, body));
    }

    [Fact]
    public async Task Schedule_ForceAllowsCompletedRerun()
    {
        AppSettingsService.Instance.Settings.EnableStartupBackfill = true;
        BackfillSyncManager.TestProviderOverride = new SlowBackfillProvider();
        BackfillSyncManager.Instance.SetStateForTests("wa-force", BackfillSyncState.Completed);

        var instance = new MessengerInstance
        {
            Id = "wa-force",
            DisplayName = "Branch",
            Platform = "testslow",
            Category = WorkspaceCategory.Professional
        };

        BackfillSyncManager.Instance.Schedule(instance, force: true);

        await WaitForStateAsync(instance.Id, BackfillSyncState.Running, TimeSpan.FromSeconds(2));
        Assert.Equal(BackfillSyncState.Running, BackfillSyncManager.Instance.GetState(instance.Id));
    }

    private static async Task WaitForStateAsync(
        string instanceId,
        BackfillSyncState expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (BackfillSyncManager.Instance.GetState(instanceId) == expected)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Expected backfill state {expected} for {instanceId}, got {BackfillSyncManager.Instance.GetState(instanceId)}.");
    }

    public void Dispose()
    {
        BackfillSyncManager.TestProviderOverride = null;
        BackfillSyncManager.Instance.ResetStateForTests();
    }

    private sealed class CountingBackfillProvider : IBackfillSyncProvider
    {
        public string PlatformId => "testcount";

        public bool CanBackfill(MessengerInstance instance) =>
            instance.Platform.Equals(PlatformId, StringComparison.OrdinalIgnoreCase);

        public Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(new BackfillResult
            {
                TriageEnqueued = 3,
                AnalyticsInboundRecorded = 3,
                TriageSkippedDuplicate = 1
            });
        }
    }

    private sealed class SlowBackfillProvider : IBackfillSyncProvider
    {
        public string PlatformId => "testslow";

        public bool CanBackfill(MessengerInstance instance) =>
            instance.Platform.Equals(PlatformId, StringComparison.OrdinalIgnoreCase);

        public async Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
        {
            _ = context;
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            return new BackfillResult();
        }
    }
}
