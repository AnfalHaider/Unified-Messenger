using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Instagram's public-activity counts (A13b) — the second, genuinely aggregate surface on this channel.
/// </summary>
public class InstagramActivityStoreTests
{
    private static string NewId() => $"ig-act-{Guid.NewGuid():N}";

    [Fact]
    public void RoundTripsTheThreeCounts()
    {
        var id = NewId();
        var at = DateTimeOffset.UtcNow;
        InstagramActivityStore.Instance.Update(id, comments: 4, likes: 5, relationships: 1, capturedAtUtc: at);

        Assert.True(InstagramActivityStore.Instance.TryGet(id, out var snapshot));
        Assert.Equal(4, snapshot.Comments);
        Assert.Equal(5, snapshot.Likes);
        Assert.Equal(1, snapshot.Relationships);
        Assert.Equal(10, snapshot.Total);
        Assert.Equal(at, snapshot.CapturedAtUtc);
    }

    [Fact]
    public void AnAccountNothingHasReadIsNullRatherThanZero()
    {
        // The distinction the whole card rests on. Zero means "we looked and there is no new activity";
        // null means "nothing here has been read". Rendering zeroes for an unread account is the same
        // false calm the sign-in gate exists to prevent.
        Assert.False(InstagramActivityStore.Instance.TryGet(NewId(), out _));
        Assert.Null(InstagramActivityStore.Instance.SumFor([NewId(), NewId()]));
        Assert.Null(InstagramActivityStore.Instance.SumFor(null));
        Assert.Null(InstagramActivityStore.Instance.SumFor([]));
    }

    [Fact]
    public void SumsAcrossAccountsAndKeepsTheNewestReadTime()
    {
        var a = NewId();
        var b = NewId();
        var older = DateTimeOffset.UtcNow.AddMinutes(-30);
        var newer = DateTimeOffset.UtcNow;

        InstagramActivityStore.Instance.Update(a, 4, 5, 1, older);
        InstagramActivityStore.Instance.Update(b, 5, 9, 8, newer);

        var total = InstagramActivityStore.Instance.SumFor([a, b]);

        Assert.NotNull(total);
        Assert.Equal(9, total!.Value.Comments);
        Assert.Equal(14, total.Value.Likes);
        Assert.Equal(9, total.Value.Relationships);

        // Least-fresh would understate how current the figure is; the card says when it was last read.
        Assert.Equal(newer, total.Value.CapturedAtUtc);
    }

    [Fact]
    public void AnUnreadAccountInTheSetDoesNotSuppressTheOnesThatWereRead()
    {
        var read = NewId();
        InstagramActivityStore.Instance.Update(read, 3, 0, 0, DateTimeOffset.UtcNow);

        var total = InstagramActivityStore.Instance.SumFor([read, NewId()]);

        Assert.NotNull(total);
        Assert.Equal(3, total!.Value.Comments);
    }

    [Fact]
    public void ZeroIsARealReadingAndSurvivesTheRoundTrip()
    {
        var id = NewId();
        InstagramActivityStore.Instance.Update(id, 0, 0, 0, DateTimeOffset.UtcNow);

        var total = InstagramActivityStore.Instance.SumFor([id]);

        // Distinct from the null case above: this account WAS read, and the honest answer is zero.
        Assert.NotNull(total);
        Assert.Equal(0, total!.Value.Total);
    }

    [Fact]
    public void NegativeCountsAreClampedRatherThanTrusted()
    {
        var id = NewId();
        InstagramActivityStore.Instance.Update(id, -3, -1, -1, DateTimeOffset.UtcNow);

        Assert.True(InstagramActivityStore.Instance.TryGet(id, out var snapshot));
        Assert.Equal(0, snapshot.Total);
    }

    [Fact]
    public void RemoveForgetsAnAccount()
    {
        var id = NewId();
        InstagramActivityStore.Instance.Update(id, 1, 1, 1, DateTimeOffset.UtcNow);
        InstagramActivityStore.Instance.Remove(id);

        Assert.False(InstagramActivityStore.Instance.TryGet(id, out _));
    }

    [Fact]
    public void TheCaveatNeverCallsTheCountATodoList()
    {
        var caveat = InstagramActivityStore.DescribeCaveat();

        // The wording is the feature. The count is unseen ACTIVITY: it clears when the notifications panel
        // is opened whether or not anyone replied, so it under-reports after a glance and must never be
        // phrased as work outstanding.
        Assert.DoesNotContain("need a reply", caveat, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unanswered", caveat, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("waiting", caveat, StringComparison.OrdinalIgnoreCase);

        // And it must say what the app cannot tell them, so nobody goes looking for a drill-down.
        Assert.Contains("clears when you look", caveat, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not say who", caveat, StringComparison.OrdinalIgnoreCase);
    }
}
