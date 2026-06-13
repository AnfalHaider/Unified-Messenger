using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public sealed class OccKanbanKeyboardReorderHelperTests
{
    [Fact]
    public void TryMoveSelection_MovesUpWithinColumn()
    {
        var ordered = new[] { "a", "b", "c" };

        var moved = OccKanbanKeyboardReorderHelper.TryMoveSelection(ordered, "b", -1, out var result);

        Assert.True(moved);
        Assert.Equal(["b", "a", "c"], result);
    }

    [Fact]
    public void TryMoveSelection_RejectsMovePastTop()
    {
        var ordered = new[] { "a", "b" };

        var moved = OccKanbanKeyboardReorderHelper.TryMoveSelection(ordered, "a", -1, out _);

        Assert.False(moved);
    }
}
