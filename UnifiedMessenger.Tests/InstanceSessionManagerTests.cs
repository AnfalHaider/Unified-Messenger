using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class InstanceSessionManagerTests
{
    [Fact]
    public void SelectLruEvictionCandidate_EvictsLeastRecentlyUsed()
    {
        var manager = new InstanceSessionManager();
        manager.TrackAccessForTests("oldest");
        manager.TrackAccessForTests("middle");
        manager.TrackAccessForTests("newest");
        manager.SetVisibleInstanceForTests("newest");

        var candidate = manager.SelectLruEvictionCandidate("incoming");

        Assert.Equal("oldest", candidate);
    }

    [Fact]
    public void SelectLruEvictionCandidate_SkipsVisibleAndIncoming()
    {
        var manager = new InstanceSessionManager();
        manager.TrackAccessForTests("keep-visible");
        manager.TrackAccessForTests("lru-target");
        manager.SetVisibleInstanceForTests("keep-visible");

        var candidate = manager.SelectLruEvictionCandidate("incoming");

        Assert.Equal("lru-target", candidate);
        Assert.Null(manager.SelectLruEvictionCandidate("lru-target"));
    }

    [Fact]
    public void SyncInstance_PreservesMemoryTierLookupAfterAccessTracking()
    {
        var manager = new InstanceSessionManager();
        var instance = new UnifiedMessenger.Models.MessengerInstance
        {
            Id = "inst-1",
            DisplayName = "Work",
            ProfileName = "slack-work",
            Platform = "slack",
            StartUrl = "https://app.slack.com/",
            MemoryTier = UnifiedMessenger.Models.MemoryTierPreference.High
        };

        manager.SyncInstance(instance);

        Assert.Equal(
            UnifiedMessenger.Models.MemoryTierPreference.High,
            manager.TryGetInstanceMemoryTier("inst-1"));
    }
}
