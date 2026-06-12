using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Persists and applies visual-only thread ordering within kanban columns and the immediate lane.
/// </summary>
public sealed class ThreadDisplayOrderService
{
    public const int UnspecifiedSortIndex = int.MaxValue;

    public const int MaxEntries = 500;

    public const string ImmediateColumnKey = "Immediate";

    private static readonly Lazy<ThreadDisplayOrderService> LazyInstance =
        new(() => new ThreadDisplayOrderService());

    private readonly Dictionary<string, Dictionary<string, int>> _sortByColumn = new(StringComparer.OrdinalIgnoreCase);

    internal bool SuppressPersistence { get; set; }

    private ThreadDisplayOrderService()
    {
    }

    public static ThreadDisplayOrderService Instance => LazyInstance.Value;

    public static string GetColumnKey(UnifiedMessengerKanbanColumn column) => column.ToString();

    public void Load(IEnumerable<ThreadDisplayOrderEntry>? entries)
    {
        _sortByColumn.Clear();
        if (entries is null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ThreadId) || string.IsNullOrWhiteSpace(entry.ColumnKey))
            {
                continue;
            }

            var columnMap = GetOrCreateColumnMap(entry.ColumnKey);
            columnMap[entry.ThreadId] = entry.SortIndex;
        }
    }

    public List<ThreadDisplayOrderEntry> Export()
    {
        var results = new List<ThreadDisplayOrderEntry>();
        foreach (var (columnKey, threadMap) in _sortByColumn)
        {
            foreach (var (threadId, sortIndex) in threadMap)
            {
                results.Add(new ThreadDisplayOrderEntry
                {
                    ThreadId = threadId,
                    ColumnKey = columnKey,
                    SortIndex = sortIndex
                });
            }
        }

        return results
            .OrderBy(entry => entry.ColumnKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SortIndex)
            .ThenBy(entry => entry.ThreadId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public int GetSortIndex(string columnKey, string threadId)
    {
        if (string.IsNullOrWhiteSpace(columnKey) || string.IsNullOrWhiteSpace(threadId))
        {
            return UnspecifiedSortIndex;
        }

        return _sortByColumn.TryGetValue(columnKey, out var threadMap) &&
               threadMap.TryGetValue(threadId, out var sortIndex)
            ? sortIndex
            : UnspecifiedSortIndex;
    }

    public void UpdateColumnOrder(string columnKey, IReadOnlyList<string> orderedThreadIds)
    {
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            return;
        }

        var columnMap = GetOrCreateColumnMap(columnKey);
        columnMap.Clear();

        for (var index = 0; index < orderedThreadIds.Count; index++)
        {
            var threadId = orderedThreadIds[index];
            if (!string.IsNullOrWhiteSpace(threadId))
            {
                columnMap[threadId] = index;
            }
        }

        TrimColumnMap(columnMap);
    }

    public void MoveToTop(string columnKey, string threadId)
    {
        if (string.IsNullOrWhiteSpace(columnKey) || string.IsNullOrWhiteSpace(threadId))
        {
            return;
        }

        var columnMap = GetOrCreateColumnMap(columnKey);
        var orderedIds = columnMap
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key)
            .Where(id => !id.Equals(threadId, StringComparison.OrdinalIgnoreCase))
            .Prepend(threadId)
            .ToList();

        UpdateColumnOrder(columnKey, orderedIds);
    }

    public void PruneOrphans(IReadOnlyCollection<string> validThreadIds)
    {
        if (validThreadIds.Count == 0)
        {
            return;
        }

        var valid = validThreadIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var columnKey in _sortByColumn.Keys.ToList())
        {
            var columnMap = _sortByColumn[columnKey];
            foreach (var threadId in columnMap.Keys.ToList())
            {
                if (!valid.Contains(threadId))
                {
                    columnMap.Remove(threadId);
                }
            }

            if (columnMap.Count == 0)
            {
                _sortByColumn.Remove(columnKey);
            }
        }
    }

    public List<ThreadData> SortThreads(
        IEnumerable<ThreadData> threads,
        string columnKey,
        Func<ThreadData, IComparable> fallbackDescending)
    {
        return threads
            .OrderBy(thread => GetSortIndex(columnKey, thread.ThreadId))
            .ThenByDescending(fallbackDescending)
            .ToList();
    }

    public List<ThreadData> SortThreadsForKanbanColumn(
        IEnumerable<ThreadData> threads,
        UnifiedMessengerKanbanColumn column) =>
        SortThreads(threads, GetColumnKey(column), thread => thread.LastMessageTime);

    public List<ThreadData> SortImmediateQueue(IEnumerable<ThreadData> threads) =>
        SortThreads(
            threads,
            ImmediateColumnKey,
            thread => thread.UrgencyScore * 1_000_000L + thread.LatencyMinutes);

    internal void ResetForTests()
    {
        _sortByColumn.Clear();
        SuppressPersistence = false;
    }

    private Dictionary<string, int> GetOrCreateColumnMap(string columnKey)
    {
        if (!_sortByColumn.TryGetValue(columnKey, out var columnMap))
        {
            columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _sortByColumn[columnKey] = columnMap;
        }

        return columnMap;
    }

    private void TrimColumnMap(Dictionary<string, int> columnMap)
    {
        if (columnMap.Count <= MaxEntries)
        {
            return;
        }

        var excess = columnMap
            .OrderByDescending(pair => pair.Value)
            .Skip(MaxEntries)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var threadId in excess)
        {
            columnMap.Remove(threadId);
        }
    }
}
