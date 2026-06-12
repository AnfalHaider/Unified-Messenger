using System.Collections.ObjectModel;
using UnifiedMessenger.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// WinUI-friendly filtered collection analogue to WPF <c>ICollectionView</c> for OCC thread cards.
/// </summary>
public sealed class OccMetricsCollectionView
{
    private readonly ObservableCollection<OperationsThreadCardViewModel> _filtered = [];

    public ReadOnlyObservableCollection<OperationsThreadCardViewModel> Filtered { get; }

    public OccMetricsCollectionView()
    {
        Filtered = new ReadOnlyObservableCollection<OperationsThreadCardViewModel>(_filtered);
    }

    public void Apply(
        IEnumerable<OperationsThreadCardViewModel> source,
        OccDateRangeFilterState filter,
        Func<OperationsThreadCardViewModel, DateTimeOffset> timestampSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(timestampSelector);

        var from = filter.FromUtc;
        var to = filter.ToUtc;
        var next = OccDateRangeFilterHelper
            .FilterByTimestamp(source, timestampSelector, from, to)
            .ToList();

        ObservableCollectionSyncHelper.Sync(
            _filtered,
            next,
            card => card.ThreadId,
            OperationsThreadCardSync.ContentEquals);
    }
}
