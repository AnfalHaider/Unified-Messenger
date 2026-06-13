namespace UnifiedMessenger.Services;

/// <summary>
/// Pure reorder logic for OCC kanban keyboard moves (Alt+Up / Alt+Down).
/// </summary>
public static class OccKanbanKeyboardReorderHelper
{
    public static bool TryMoveSelection(
        IReadOnlyList<string> orderedThreadIds,
        string? selectedThreadId,
        int delta,
        out IReadOnlyList<string> newOrder)
    {
        newOrder = orderedThreadIds;

        if (string.IsNullOrWhiteSpace(selectedThreadId) || orderedThreadIds.Count < 2 || delta == 0)
        {
            return false;
        }

        var list = orderedThreadIds.ToList();
        var index = list.FindIndex(id => id.Equals(selectedThreadId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        var targetIndex = index + delta;
        if (targetIndex < 0 || targetIndex >= list.Count)
        {
            return false;
        }

        (list[index], list[targetIndex]) = (list[targetIndex], list[index]);
        newOrder = list;
        return true;
    }
}
