using UnifiedMessenger.Services;
using Windows.System;

namespace UnifiedMessenger.Tests;

public class KeyboardShortcutServiceTests
{
    [Fact]
    public void ShouldHandleShortcut_WhenCanExecuteNull_ReturnsTrue()
    {
        Assert.True(KeyboardShortcutService.ShouldHandleShortcut(null));
    }

    [Fact]
    public void ShouldHandleShortcut_WhenCanExecuteTrue_ReturnsTrue()
    {
        Assert.True(KeyboardShortcutService.ShouldHandleShortcut(() => true));
    }

    [Fact]
    public void ShouldHandleShortcut_WhenCanExecuteFalse_ReturnsFalse()
    {
        Assert.False(KeyboardShortcutService.ShouldHandleShortcut(() => false));
    }

    [Theory]
    [InlineData(0, VirtualKey.Number1)]
    [InlineData(8, VirtualKey.Number9)]
    public void ResolveIndexedShortcutKey_MapsCtrlNumberShortcuts(int index, VirtualKey expected)
    {
        Assert.Equal(expected, KeyboardShortcutService.ResolveIndexedShortcutKey(VirtualKey.Number1, index));
    }

    [Fact]
    public void ResolveIndexedShortcutKey_RejectsOutOfRangeIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeyboardShortcutService.ResolveIndexedShortcutKey(VirtualKey.Number1, 9));
    }
}
