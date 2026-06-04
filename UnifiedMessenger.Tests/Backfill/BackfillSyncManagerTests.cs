using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests.Backfill;

public class BackfillSyncManagerTests : IDisposable
{
    private readonly bool _originalEnableBackfill;

    public BackfillSyncManagerTests()
    {
        _originalEnableBackfill = AppSettingsService.Instance.Settings.EnableStartupBackfill;
        BackfillSyncManager.Instance.ResetStateForTests();
        BackfillSyncManager.TestProviderOverride = null;
    }

    [Fact]
    public void Schedule_SkipsWhenStartupBackfillDisabled()
    {
        AppSettingsService.Instance.Settings.EnableStartupBackfill = false;

        var instance = CreateProfessionalInstance("wa-1", "whatsapp");
        BackfillSyncManager.Instance.Schedule(instance);

        Assert.Equal(BackfillSyncState.Skipped, BackfillSyncManager.Instance.GetState(instance.Id));
    }

    [Fact]
    public void Schedule_SkipsPersonalInstances()
    {
        AppSettingsService.Instance.Settings.EnableStartupBackfill = true;

        var instance = new MessengerInstance
        {
            Id = "wa-personal",
            DisplayName = "WhatsApp",
            Platform = "whatsapp",
            Category = WorkspaceCategory.Personal
        };

        BackfillSyncManager.Instance.Schedule(instance);

        Assert.Equal(BackfillSyncState.Skipped, BackfillSyncManager.Instance.GetState(instance.Id));
    }

    [Fact]
    public async Task RunBackfillForTestsAsync_TimeoutSetsFailedState()
    {
        AppSettingsService.Instance.Settings.EnableStartupBackfill = true;
        BackfillSyncManager.TestProviderOverride = new StallingBackfillProvider();

        var instance = CreateProfessionalInstance("stall-1", "teststall");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var result = await BackfillSyncManager.Instance.RunBackfillForTestsAsync(instance, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackfillSyncState.Failed, BackfillSyncManager.Instance.GetState(instance.Id));
    }

    [Fact]
    public async Task RunBackfillForTestsAsync_CompletesWithTestProvider()
    {
        BackfillSyncManager.TestProviderOverride = new ImmediateBackfillProvider();

        var instance = CreateProfessionalInstance("ok-1", "testok");
        var result = await BackfillSyncManager.Instance.RunBackfillForTestsAsync(instance);

        Assert.True(result.IsSuccess);
        Assert.Equal(BackfillSyncState.Completed, BackfillSyncManager.Instance.GetState(instance.Id));
        Assert.Equal(2, result.TriageEnqueued);
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.EnableStartupBackfill = _originalEnableBackfill;
        BackfillSyncManager.TestProviderOverride = null;
        BackfillSyncManager.Instance.ResetStateForTests();
    }

    private static MessengerInstance CreateProfessionalInstance(string id, string platform) =>
        new()
        {
            Id = id,
            DisplayName = "Professional",
            Platform = platform,
            Category = WorkspaceCategory.Professional
        };

    private sealed class StallingBackfillProvider : IBackfillSyncProvider
    {
        public string PlatformId => "teststall";

        public bool CanBackfill(MessengerInstance instance) =>
            instance.Platform.Equals(PlatformId, StringComparison.OrdinalIgnoreCase);

        public async Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
        {
            _ = context;
            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
            return new BackfillResult();
        }
    }

    private sealed class ImmediateBackfillProvider : IBackfillSyncProvider
    {
        public string PlatformId => "testok";

        public bool CanBackfill(MessengerInstance instance) =>
            instance.Platform.Equals(PlatformId, StringComparison.OrdinalIgnoreCase);

        public Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(new BackfillResult { TriageEnqueued = 2 });
        }
    }
}
