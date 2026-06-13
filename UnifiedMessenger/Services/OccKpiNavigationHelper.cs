using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public enum OccKpiKind
{
    OpenThreads,
    HangingLeads,
    NeedsAction,
    SlaBreaches
}

public sealed class OccKpiNavigationTarget
{
    public required string InstanceId { get; init; }

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }
}

public static class OccKpiNavigationHelper
{
    public static OccKpiNavigationTarget? ResolveTarget(
        OccKpiKind kind,
        IEnumerable<ThreadData> threads,
        ThreadDisplayOrderService? displayOrder = null)
    {
        ArgumentNullException.ThrowIfNull(threads);

        var order = displayOrder ?? ThreadDisplayOrderService.Instance;
        var actionable = threads
            .Where(thread => !thread.IsSpamOrPromo)
            .ToList();

        ThreadData? selected = kind switch
        {
            OccKpiKind.OpenThreads => SelectHighestLatencyUnreplied(actionable),
            OccKpiKind.HangingLeads => SelectTopKanbanThread(
                actionable,
                UnifiedMessengerKanbanColumn.HangingLeads,
                order),
            OccKpiKind.NeedsAction => SelectTopImmediateQueueThread(actionable, order),
            OccKpiKind.SlaBreaches => SelectWorstSlaBreach(actionable),
            _ => null
        };

        if (selected is null || string.IsNullOrWhiteSpace(selected.InstanceId))
        {
            return null;
        }

        return new OccKpiNavigationTarget
        {
            InstanceId = selected.InstanceId,
            ConversationKey = string.IsNullOrWhiteSpace(selected.ConversationKey)
                ? null
                : selected.ConversationKey,
            CustomerName = selected.CustomerName
        };
    }

    private static ThreadData? SelectHighestLatencyUnreplied(IReadOnlyList<ThreadData> threads) =>
        threads
            .Where(thread => !thread.IsReplied)
            .OrderByDescending(thread => thread.LatencyMinutes)
            .ThenByDescending(thread => thread.UrgencyScore)
            .ThenByDescending(thread => thread.LastMessageTime)
            .FirstOrDefault();

    private static ThreadData? SelectTopKanbanThread(
        IReadOnlyList<ThreadData> threads,
        UnifiedMessengerKanbanColumn column,
        ThreadDisplayOrderService displayOrder) =>
        displayOrder
            .SortThreadsForKanbanColumn(
                threads.Where(thread => thread.KanbanColumn == column),
                column)
            .FirstOrDefault();

    private static ThreadData? SelectTopImmediateQueueThread(
        IReadOnlyList<ThreadData> threads,
        ThreadDisplayOrderService displayOrder) =>
        displayOrder
            .SortImmediateQueue(threads.Where(thread => thread.IsImmediateAction && !thread.IsReplied))
            .FirstOrDefault();

    private static ThreadData? SelectWorstSlaBreach(IReadOnlyList<ThreadData> threads) =>
        threads
            .Where(thread => thread.IsSlaBreached)
            .OrderByDescending(thread => thread.LatencyMinutes)
            .ThenByDescending(thread => thread.UrgencyScore)
            .ThenByDescending(thread => thread.LastMessageTime)
            .FirstOrDefault();
}
