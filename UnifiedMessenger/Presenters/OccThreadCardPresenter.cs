using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;

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
        bool hideBranchName = false,
        AiInferenceQueue? inferenceQueue = null,
        bool compactDensity = false) =>
        threads
            .Select(thread => BuildThreadCard(thread, hideBranchName, inferenceQueue, compactDensity))
            .ToList();

    public static OperationsThreadCardViewModel BuildThreadCard(
        ThreadData thread,
        bool hideBranchName = false,
        AiInferenceQueue? inferenceQueue = null,
        bool compactDensity = false)
    {
        var source = ResolveInferenceSource(thread, inferenceQueue);
        return new OperationsThreadCardViewModel(thread, hideBranchName, source, compactDensity);
    }

    public static IEnumerable<OperationsThreadCardViewModel> BuildKanbanColumn(
        IEnumerable<ThreadData> allThreads,
        UnifiedMessengerKanbanColumn column,
        bool hideBranchName = false,
        AiInferenceQueue? inferenceQueue = null,
        bool compactDensity = false) =>
        BuildThreadCards(FilterKanbanColumn(allThreads, column), hideBranchName, inferenceQueue, compactDensity);

    internal static TriageInferenceSource ResolveInferenceSource(
        ThreadData thread,
        AiInferenceQueue? inferenceQueue)
    {
        if (inferenceQueue is not null &&
            inferenceQueue.IsThreadInferenceActive(thread.ThreadId))
        {
            return TriageInferenceSource.Analyzing;
        }

        return thread.InferenceSource;
    }
}
