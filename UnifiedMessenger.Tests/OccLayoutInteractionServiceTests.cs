using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OccLayoutInteractionServiceTests
{
    private readonly OccLayoutInteractionService _service = new();

    [Fact]
    public void TryNudgePanel_MovesPanelWithinGrid()
    {
        var placements = OccLayoutPresets.CreateOperationsFocus();
        var panelId = OccLayoutDefaults.ImmediateLanePanelId;
        var before = placements.First(p => p.PanelId == panelId).Column;

        var moved = _service.TryNudgePanel(placements, panelId, 1, out var updated);

        Assert.True(moved);
        var after = updated.First(p => p.PanelId == panelId).Column;
        Assert.Equal(before + 1, after);
    }
}
