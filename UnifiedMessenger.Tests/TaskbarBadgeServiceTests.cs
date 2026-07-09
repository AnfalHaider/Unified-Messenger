using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class TaskbarBadgeServiceTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(99, 99)]
    [InlineData(150, 99)]
    public void NormalizeBadgeCount_ClampsToValidRange(int input, int expected)
    {
        Assert.Equal(expected, TaskbarBadgeService.NormalizeBadgeCount(input));
    }

    [Theory]
    [InlineData(true, 3, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 5, false)]
    public void ShouldDisplayBadge_RespectsToggleAndCount(bool showBadge, int count, bool expected)
    {
        var settings = new AppSettings { ShowTaskbarBadge = showBadge };

        Assert.Equal(expected, TaskbarBadgeService.ShouldDisplayBadge(settings, count));
    }

    [Fact]
    public void NormalizeOverlayCount_MatchesBadgeClampRules()
    {
        Assert.Equal(99, TaskbarOverlayService.NormalizeOverlayCount(250));
        Assert.Equal(0, TaskbarOverlayService.NormalizeOverlayCount(0));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(5, "5")]
    [InlineData(99, "99")]
    [InlineData(250, "99")]
    public void FormatOverlayLabel_MatchesNormalizedCount(int count, string expected) =>
        Assert.Equal(expected, TaskbarOverlayService.FormatOverlayLabel(count));

    [Fact]
    public void TryCreateCountIcon_ReturnsHandleForPositiveCounts()
    {
        Assert.True(TaskbarOverlayIconRenderer.TryCreateCountIcon(7, out var iconHandle));
        Assert.NotEqual(IntPtr.Zero, iconHandle);
        TaskbarOverlayIconRenderer.DestroyIconHandle(iconHandle);
    }

    [Fact]
    public void TryCreateCountIcon_ReturnsFalseForZeroCount()
    {
        Assert.False(TaskbarOverlayIconRenderer.TryCreateCountIcon(0, out var iconHandle));
        Assert.Equal(IntPtr.Zero, iconHandle);
    }
}
