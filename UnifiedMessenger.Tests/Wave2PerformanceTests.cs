using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class Wave2PerformanceTests
{
    [Fact]
    public void GetAllThreads_ReusesSortedCacheUntilMutation()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.UpsertFromTriageItem(
            new MessageTriageItem
            {
                Id = "t1",
                InstanceId = "inst-1",
                InstanceDisplayName = "Work",
                Platform = "slack",
                CustomerName = "Alex",
                MessagePreview = "Hello",
                TimestampUtc = DateTimeOffset.UtcNow
            },
            conversationKey: "alex",
            branchName: null);

        var first = registry.GetAllThreads();
        var second = registry.GetAllThreads();

        Assert.Same(first, second);
    }

    [Fact]
    public void RefreshOperationalFlags_WithRaiseChangedFalse_DoesNotFireChanged()
    {
        var registry = ThreadRegistryService.CreateForTests();
        registry.UpsertFromTriageItem(
            new MessageTriageItem
            {
                Id = "t1",
                InstanceId = "inst-1",
                InstanceDisplayName = "Work",
                Platform = "slack",
                CustomerName = "Alex",
                MessagePreview = "Hello",
                TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-30)
            },
            conversationKey: "alex",
            branchName: null);

        var changes = 0;
        registry.Changed += (_, _) => changes++;

        registry.RefreshOperationalFlags(raiseChanged: false);

        Assert.Equal(0, changes);
        Assert.True(registry.GetAllThreads()[0].LatencyMinutes > 0);
    }

    [Fact]
    public void ObservableCollectionSyncHelper_ReusesUnchangedItems()
    {
        var target = new System.Collections.ObjectModel.ObservableCollection<string>(["a", "b"]);
        var replacements = 0;
        target.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                replacements++;
            }
        };

        ObservableCollectionSyncHelper.Sync(
            target,
            ["a", "b"],
            static item => item,
            static (left, right) => left == right);

        Assert.Equal(0, replacements);
        Assert.Equal(["a", "b"], target);
    }

    [Fact]
    public async Task CreateDefaultSettings_UsesPerformanceFriendlyStartupDefaults()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var storePath = Path.Combine(tempDirectory, "settings.json");

        try
        {
            var service = new AppSettingsService(storePath);
            await service.LoadAsync();

            Assert.Equal(6, service.Settings.MaxConcurrentWebViews);
            Assert.Equal(StartupWarmMode.VisibleOnly, service.Settings.StartupWarmMode);
            Assert.True(service.Settings.EnableLazyWebViewLoading);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
