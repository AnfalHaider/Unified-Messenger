using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Presenters;

public static class OccThreadCardPresenter
{
    public static IReadOnlyList<ThreadData> FilterKanbanColumn(
        IEnumerable<ThreadData> threads,
        UnifiedMessengerKanbanColumn column) =>
        threads
            .Where(thread => thread.KanbanColumn == column)
            .ToList();

    public static IReadOnlyList<OperationsThreadCardViewModel> BuildThreadCards(
        IEnumerable<ThreadData> threads,
        bool hideBranchName = false) =>
        threads
            .Select(thread => new OperationsThreadCardViewModel(thread, hideBranchName))
            .ToList();

    public static IEnumerable<OperationsThreadCardViewModel> BuildKanbanColumn(
        IEnumerable<ThreadData> allThreads,
        UnifiedMessengerKanbanColumn column,
        bool hideBranchName = false) =>
        BuildThreadCards(FilterKanbanColumn(allThreads, column), hideBranchName);
}
