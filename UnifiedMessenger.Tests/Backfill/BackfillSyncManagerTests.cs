using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests.Backfill;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class BackfillSyncManagerTests : IDisposable
{
    public BackfillSyncManagerTests()
    {
        BackfillSyncManager.Instance.ResetStateForTests();
        BackfillSyncManager.TestProviderOverride = null;
        AppSettingsService.Instance.Settings.EnableStartupBackfill = true;
    }

    [Theory]
    [InlineData("whatsapp", true)]
    [InlineData("whatsappbusiness", true)]
    [InlineData("metabusiness", false)]
    public void WhatsAppBackfillProvider_CanBackfillWhatsAppPlatforms(string platform, bool expected)
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

    [Fact]
    public void Schedule_SkipsWhenStartupBackfillDisabled()
    {
        AppSettingsService.Instance.Settings.EnableStartupBackfill = false;
        var instance = new MessengerInstance
        {
            Id = "wa-1",
            DisplayName = "Branch",
            Platform = "whatsapp",
            Category = WorkspaceCategory.Professional
        };

        BackfillSyncManager.Instance.Schedule(instance);

        Assert.Equal(BackfillSyncState.Skipped, BackfillSyncManager.Instance.GetState(instance.Id));
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
        Assert.Equal(2, result.TriageEnqueued);
        Assert.Equal(2, BackfillSyncManager.Instance.GetLastResult(instance.Id)?.TriageEnqueued);
        Assert.NotNull(progress);
        Assert.Equal(BackfillSyncState.Completed, BackfillSyncManager.Instance.GetState(instance.Id));
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
            instance.Platform.Equals("testcount", StringComparison.OrdinalIgnoreCase);

        public Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
        {
            _ = context;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new BackfillResult { TriageEnqueued = 2 });
        }
    }
}
