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

    /// <summary>
    /// The taskbar overlay must ask for the ITaskbarList3 INTERFACE id, not the TaskbarList class id.
    /// </summary>
    /// <remarks>
    /// It carried the CLSID for the life of the feature, so every badge update created the taskbar object
    /// correctly and then failed QueryInterface with E_NOINTERFACE. Combined with the Windows App SDK
    /// badge API not working in this app's unpackaged self-contained configuration, that meant the taskbar
    /// badge never worked at all — while Settings offered a toggle for it. The two ids differ by one
    /// character of intent and nothing else, which is exactly why this is worth pinning.
    /// </remarks>
    [Fact]
    public void TaskbarOverlay_AsksForTheInterfaceIdNotTheClassId()
    {
        var iface = typeof(TaskbarOverlayService)
            .GetNestedType("ITaskbarList3", System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(iface);

        var guid = iface!.GUID;

        // IID_ITaskbarList3, as published by Windows.
        Assert.Equal(new Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), guid);

        // CLSID_TaskbarList — the value that used to be here, and the one the failure named.
        Assert.NotEqual(new Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), guid);
    }
}
