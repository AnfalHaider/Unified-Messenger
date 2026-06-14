using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Shared work-queue filter chip state for the Operations Command Center.
/// </summary>
public sealed class OccQueueFilterState
{
    private static readonly Lazy<OccQueueFilterState> LazyInstance = new(() => new OccQueueFilterState());

    private OccQueueFilter _filter = OccQueueFilter.AllOpen;

    private OccQueueFilterState()
    {
    }

    public static OccQueueFilterState Instance => LazyInstance.Value;

    internal static OccQueueFilterState CreateForTests() => new();

    public event EventHandler? Changed;

    public OccQueueFilter Filter
    {
        get => _filter;
        set
        {
            if (_filter == value)
            {
                return;
            }

            _filter = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
