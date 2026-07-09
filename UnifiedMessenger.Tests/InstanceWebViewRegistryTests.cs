using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class InstanceWebViewRegistryTests
{
    [Fact]
    public void TrackProfileForTests_EnforcesUniqueProfileNames()
    {
        var registry = new InstanceWebViewRegistry();

        registry.TrackProfileForTests("instance-a", "profile-a");

        Assert.Equal("instance-a", registry.GetOwnerInstanceId("profile-a"));
        Assert.Throws<InvalidOperationException>(() =>
            registry.TrackProfileForTests("instance-b", "profile-a"));
    }

    [Fact]
    public void Unregister_ReleasesProfileOwnership()
    {
        var registry = new InstanceWebViewRegistry();
        registry.TrackProfileForTests("instance-a", "profile-a");

        registry.Unregister("instance-a");

        Assert.Null(registry.GetOwnerInstanceId("profile-a"));
        Assert.False(registry.Contains("instance-a"));
    }

    [Fact]
    public void ReleaseProfile_RemovesTrackedOwnership()
    {
        var registry = new InstanceWebViewRegistry();
        registry.TrackProfileForTests("instance-a", "profile-a");

        registry.ReleaseProfile("profile-a");

        Assert.Null(registry.GetOwnerInstanceId("profile-a"));
        Assert.False(registry.IsProfileOwnedByOther("profile-a", "instance-a"));
    }
}
