using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Shared view mode for Operations Command Center operational panels.
/// Live mode keeps KPI/kanban on current workload; Historical filters by date range.
/// </summary>
public sealed class OccViewModeState
{
    private static readonly Lazy<OccViewModeState> LazyInstance = new(() => new OccViewModeState());

    private OccViewMode _mode = OccViewMode.Live;

    private OccViewModeState()
    {
    }

    public static OccViewModeState Instance => LazyInstance.Value;

    internal static OccViewModeState CreateForTests() => new();

    public event EventHandler? Changed;

    public OccViewMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsHistorical => Mode == OccViewMode.Historical;
}
