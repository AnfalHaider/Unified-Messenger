namespace UnifiedMessenger.Services;

/// <summary>
/// Shared branch filter state for the Operations Command Center filter chip and cross-page navigation.
/// </summary>
public sealed class OccFilterState
{
    private static readonly Lazy<OccFilterState> LazyInstance = new(() => new OccFilterState());

    private string? _branchKey;

    private OccFilterState()
    {
    }

    public static OccFilterState Instance => LazyInstance.Value;

    internal static OccFilterState CreateForTests() => new();

    public event EventHandler? Changed;

    public string? BranchKey
    {
        get => _branchKey;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (string.Equals(_branchKey, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _branchKey = normalized;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear() => BranchKey = null;
}
