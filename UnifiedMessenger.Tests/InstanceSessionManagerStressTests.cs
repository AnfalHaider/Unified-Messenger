using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// P3-C stress fixtures: confirm the WebView2 memory strategy's *policy* holds at many-instance scale.
/// The session manager itself spawns real WebViews (can't run headless), but the eviction/reap decisions
/// are pure logic — these exercise that logic across hundreds of instances so a regression in the LRU
/// ordering or the reap-exemption rules fails the build instead of leaking RAM in production.
/// </summary>
public class InstanceSessionManagerStressTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);

    [Fact]
    public void SelectLruEvictionCandidate_EvictsStrictLruOrderAcrossManyInstances()
    {
        const int count = 200;
        var manager = new InstanceSessionManager();

        // Access in ascending order so "inst-000" is the least-recently-used, "inst-199" the most recent.
        for (var i = 0; i < count; i++)
        {
            manager.TrackAccessForTests($"inst-{i:000}");
        }

        var visible = "inst-199";
        var incoming = "inst-incoming";
        manager.SetVisibleInstanceForTests(visible);

        // Simulate repeated cap enforcement: each round evicts the current LRU candidate. The order must be
        // strictly oldest-first, and neither the visible nor the (hypothetical) incoming session is ever chosen.
        for (var expected = 0; expected < count - 1; expected++) // -1: the visible one is never evicted
        {
            var candidate = manager.SelectLruEvictionCandidate(incoming);
            Assert.Equal($"inst-{expected:000}", candidate);
            Assert.NotEqual(visible, candidate);
            Assert.NotEqual(incoming, candidate);
            manager.RemoveAccessForTests(candidate!);
        }

        // Only the protected visible session remains → nothing left to evict.
        Assert.Null(manager.SelectLruEvictionCandidate(incoming));
    }

    [Fact]
    public void SelectLruEvictionCandidate_NeverEvictsVisibleEvenWhenItIsOldest()
    {
        var manager = new InstanceSessionManager();
        manager.TrackAccessForTests("pinned-visible"); // oldest
        for (var i = 0; i < 50; i++)
        {
            manager.TrackAccessForTests($"bg-{i:00}");
        }

        manager.SetVisibleInstanceForTests("pinned-visible");

        // Evict everything evictable; the visible (oldest) must survive every round.
        for (var i = 0; i < 50; i++)
        {
            var candidate = manager.SelectLruEvictionCandidate("incoming");
            Assert.NotNull(candidate);
            Assert.NotEqual("pinned-visible", candidate);
            manager.RemoveAccessForTests(candidate!);
        }

        Assert.Null(manager.SelectLruEvictionCandidate("incoming"));
    }

    [Fact]
    public void IsReapEligible_HoldsExemptionInvariantsAcrossLargeMatrix()
    {
        // Exhaustively sweep the decision space: a session is reapable ONLY when it is non-visible,
        // non-professional, has been accessed, and that access is at least the TTL ago.
        for (var ageMinutes = 0; ageMinutes <= 120; ageMinutes++)
        {
            var lastAccess = Now - TimeSpan.FromMinutes(ageMinutes);
            foreach (var isVisible in new[] { true, false })
            foreach (var isProfessional in new[] { true, false })
            foreach (var hasAccess in new[] { true, false })
            {
                var stamp = hasAccess ? lastAccess : (DateTimeOffset?)null;
                var actual = InstanceSessionManager.IsReapEligible(isVisible, isProfessional, stamp, Now, Ttl);

                var expected = !isVisible
                               && !isProfessional
                               && hasAccess
                               && ageMinutes >= Ttl.TotalMinutes;

                Assert.Equal(expected, actual);
            }
        }
    }
}
