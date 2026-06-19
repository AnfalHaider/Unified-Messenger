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
    public void SelectLruEvictionCandidate_ReturnsNullWhenOnlyProtectedInstancesRemain()
    {
        var manager = new InstanceSessionManager();
        manager.TrackAccessForTests("visible-only");
        manager.SetVisibleInstanceForTests("visible-only");

        Assert.Null(manager.SelectLruEvictionCandidate("visible-only"));
    }

    private static readonly DateTimeOffset Now = new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);

    [Fact]
    public void IsReapEligible_ReapsIdleNonVisiblePersonalSession()
    {
        var lastAccess = Now - TimeSpan.FromMinutes(21);

        Assert.True(InstanceSessionManager.IsReapEligible(
            isVisible: false, isProfessional: false, lastAccess, Now, Ttl));
    }

    [Fact]
    public void IsReapEligible_KeepsSessionIdleLessThanTtl()
    {
        var lastAccess = Now - TimeSpan.FromMinutes(19);

        Assert.False(InstanceSessionManager.IsReapEligible(
            isVisible: false, isProfessional: false, lastAccess, Now, Ttl));
    }

    [Fact]
    public void IsReapEligible_NeverReapsVisibleSession()
    {
        var lastAccess = Now - TimeSpan.FromHours(5);

        Assert.False(InstanceSessionManager.IsReapEligible(
            isVisible: true, isProfessional: false, lastAccess, Now, Ttl));
    }

    [Fact]
    public void IsReapEligible_NeverReapsProfessionalAccount()
    {
        // Professional accounts stay live so background oversight keeps reading them.
        var lastAccess = Now - TimeSpan.FromHours(5);

        Assert.False(InstanceSessionManager.IsReapEligible(
            isVisible: false, isProfessional: true, lastAccess, Now, Ttl));
    }

    [Fact]
    public void IsReapEligible_DoesNotReapNeverAccessedSession()
    {
        Assert.False(InstanceSessionManager.IsReapEligible(
            isVisible: false, isProfessional: false, lastAccess: null, Now, Ttl));
    }

    [Fact]
    public void IsReapEligible_ReapsExactlyAtTtlBoundary()
    {
        var lastAccess = Now - Ttl;

        Assert.True(InstanceSessionManager.IsReapEligible(
            isVisible: false, isProfessional: false, lastAccess, Now, Ttl));
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
