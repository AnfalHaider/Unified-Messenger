using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression cover for T-15: the test suite was writing into the developer's live oversight data.
///
/// <para>
/// The suite exercises the real singletons — <c>OversightChatSnapshotService.Instance</c>,
/// <c>ResponseTimeTracker.Instance</c>, <c>ContactHistoryStore.Instance</c>,
/// <c>MessageAnalyticsService.Instance</c> — and every one of them resolves its file path from
/// <see cref="ApplicationPaths.UserDataRoot"/>. So <c>AwaitingSplitTests</c> calling <c>svc.Update(...)</c>
/// wrote fabricated chats into <c>%LOCALAPPDATA%\UnifiedMessenger</c>. A scan of the real store found the
/// test account id <c>inst-1</c> filed alongside the owner's real accounts.
/// </para>
/// <para>
/// The junk rows were the harmless half. <c>OversightChatSnapshotService.Update</c> reaches
/// <c>ResponseTimeTracker.Observe</c>, which stamps a per-account watch start the first time it sees an
/// account and thereafter measures only replies to messages that arrived after it. Every run of the suite
/// pushed that stamp forward, disqualifying every conversation already in flight — which is why the real
/// store could hold 761 KB of scraped snapshot and 218 KB of contact history and still contain zero
/// reply-time samples. "Median reply time" and "SLA met" were computed from that.
/// </para>
/// </summary>
public class TestIsolationTests
{
    private static string RealUserDataRoot => ApplicationPaths.DefaultUserDataRoot;

    [Fact]
    public void TheSuiteDoesNotWriteIntoTheRealUserDataRoot()
    {
        Assert.NotNull(ApplicationPaths.UserDataRootOverrideForTests);
        Assert.NotEqual(RealUserDataRoot, ApplicationPaths.UserDataRoot, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The derived paths are what the stores actually open, so assert those rather than only the root —
    /// a future property that rebuilds its path from <c>Environment.GetFolderPath</c> directly would slip
    /// past a root-only check.
    /// </summary>
    [Fact]
    public void EveryDerivedStorePathStaysInsideTheOverride()
    {
        var root = ApplicationPaths.UserDataRoot;

        Assert.StartsWith(root, ApplicationPaths.SettingsFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(root, ApplicationPaths.InstancesFilePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The singletons the suite actually touches. If one of these ever resolves back to the real root, the
    /// owner's oversight data is being overwritten by a test run again.
    /// </summary>
    [Fact]
    public void TheLiveSingletonsWriteToTheOverriddenRoot()
    {
        var real = RealUserDataRoot;
        var names = new[]
        {
            "oversight-snapshot.json", "response-times.json", "contact-history.json", "analytics.json"
        };

        // Snapshot first, act, compare. Not "written in the last minute" — on a developer machine the real
        // app may legitimately have written a second ago, which would fail a wall-clock window for a reason
        // that has nothing to do with this suite.
        DateTime Stamp(string name)
        {
            var path = Path.Combine(real, name);
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }

        var before = names.ToDictionary(n => n, Stamp);

        // Forces the lazy path resolution that is the thing under test — the store paths are captured in
        // static construction, so they must resolve after the module initializer has run. This is the exact
        // call AwaitingSplitTests makes, and the one that reached ResponseTimeTracker.Observe.
        OversightChatSnapshotService.Instance.Update("isolation-probe", [], DateTimeOffset.UtcNow);
        Thread.Sleep(50);

        foreach (var name in names)
        {
            Assert.True(
                Stamp(name) == before[name],
                $"'{name}' in the real user-data root was written by this test. The suite is writing into "
                + "live oversight data again — check ApplicationPaths.UserDataRootOverrideForTests.");
        }
    }
}
