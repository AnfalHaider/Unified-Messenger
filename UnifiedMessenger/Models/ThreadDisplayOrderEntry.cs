namespace UnifiedMessenger.Models;

/// <summary>
/// Manual display priority for a thread within a kanban column or the immediate action lane.
/// Column membership remains computed from operational flags; only sort order is user-controlled.
/// </summary>
public sealed class ThreadDisplayOrderEntry
{
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="UnifiedMessengerKanbanColumn"/> name or <c>Immediate</c> for the urgent queue.
    /// </summary>
    public string ColumnKey { get; set; } = string.Empty;

    public int SortIndex { get; set; }
}
