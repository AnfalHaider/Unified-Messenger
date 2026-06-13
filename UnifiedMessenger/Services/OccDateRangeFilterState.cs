namespace UnifiedMessenger.Services;

/// <summary>
/// Shared date-range filter for Operations Command Center charts and analytics.
/// KPI, kanban, and immediate queue always show current workload regardless of range.
/// </summary>
public sealed class OccDateRangeFilterState
{
    public const int DefaultWindowDays = 7;

    private static readonly Lazy<OccDateRangeFilterState> LazyInstance = new(() => new OccDateRangeFilterState());

    private DateTimeOffset? _fromUtc;
    private DateTimeOffset? _toUtc;

    private OccDateRangeFilterState()
    {
    }

    public static OccDateRangeFilterState Instance => LazyInstance.Value;

    internal static OccDateRangeFilterState CreateForTests() => new();

    public event EventHandler? Changed;

    /// <summary>Inclusive start of range (UTC midnight of selected local date).</summary>
    public DateTimeOffset? FromUtc
    {
        get => _fromUtc;
        set
        {
            var normalized = NormalizeStartOfDay(value);
            if (_fromUtc == normalized)
            {
                return;
            }

            _fromUtc = normalized;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Inclusive end of range (UTC end of selected local date).</summary>
    public DateTimeOffset? ToUtc
    {
        get => _toUtc;
        set
        {
            var normalized = NormalizeEndOfDay(value);
            if (_toUtc == normalized)
            {
                return;
            }

            _toUtc = normalized;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HasActiveFilter => FromUtc is not null && ToUtc is not null;

    public void Clear() => ResetToDefaultWindow();

    public void ResetToDefaultWindow()
    {
        var today = DateTimeOffset.Now;
        var from = NormalizeStartOfDay(today.AddDays(-(DefaultWindowDays - 1)));
        var to = NormalizeEndOfDay(today);

        if (_fromUtc == from && _toUtc == to)
        {
            return;
        }

        _fromUtc = from;
        _toUtc = to;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyPersistedRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        if (fromUtc is null || toUtc is null)
        {
            ResetToDefaultWindow();
            return;
        }

        var from = NormalizeStartOfDay(fromUtc);
        var to = NormalizeEndOfDay(toUtc);
        if (from is null || to is null)
        {
            ResetToDefaultWindow();
            return;
        }

        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (_fromUtc == from && _toUtc == to)
        {
            return;
        }

        _fromUtc = from;
        _toUtc = to;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static DateTimeOffset? NormalizeStartOfDay(DateTimeOffset? value) =>
        value is { } v ? new DateTimeOffset(v.LocalDateTime.Date, v.Offset) : null;

    public static DateTimeOffset? NormalizeEndOfDay(DateTimeOffset? value) =>
        value is { } v
            ? new DateTimeOffset(v.LocalDateTime.Date.AddDays(1).AddTicks(-1), v.Offset)
            : null;
}
