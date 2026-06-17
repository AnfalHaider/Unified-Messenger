namespace UnifiedMessenger.Models;

/// <summary>Whether the command center is rolling up by individual account or by location.</summary>
public enum OversightGrouping
{
    ByInstance,
    ByLocation
}

public enum OversightEntityKind
{
    Instance,
    Location
}

/// <summary>Date scope for the command center. On-time is measured over conversations active in the window.</summary>
public enum OversightWindow
{
    Today,
    Week,
    All
}

/// <summary>
/// One health card in the oversight command center — an account or a location — with the glanceable
/// numbers the dashboard surfaces (on-time %, urgent, dropped, freshness).
/// </summary>
public sealed class OversightEntityHealth
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public OversightEntityKind Kind { get; init; }

    /// <summary>Number of accounts rolled into this entity (1 for an account; N for a location).</summary>
    public int AccountCount { get; init; } = 1;

    public int OpenCount { get; init; }

    /// <summary>
    /// Number of LIVE (non-backfilled) threads the on-time % is computed over. 0 means there is no
    /// live responsiveness data yet — the UI should say "no live data" rather than show a misleading %.
    /// </summary>
    public int MeasuredCount { get; init; }

    /// <summary>Open threads carried over from history (backfilled) — shown separately, not as breaches.</summary>
    public int HistoricalOpenCount { get; init; }

    /// <summary>Share of LIVE actionable threads replied within (or still inside) the SLA, 0–100.</summary>
    public int OnTimePercent { get; init; } = 100;

    public int UrgentCount { get; init; }

    public int DroppedCount { get; init; }

    public bool IsStale { get; init; }

    public DateTimeOffset? LastActivityUtc { get; init; }

    /// <summary>Instance ids rolled into this entity — used to expand a location into its accounts.</summary>
    public IReadOnlyList<string> MemberInstanceIds { get; init; } = [];

    /// <summary>
    /// Recent-activity sparkline: 7 buckets (oldest → newest, one per day ending today) counting
    /// actionable threads last active that day. A glanceable trend, derived from live threads — no
    /// historical store required.
    /// </summary>
    public IReadOnlyList<int> TrendCounts { get; init; } = [];
}

/// <summary>
/// The all-entities command-center rollup: worst-first entities plus a cross-entity "needs attention"
/// summary that answers the owner's first question — who's waiting, where.
/// </summary>
public sealed class OversightCommandCenterSnapshot
{
    public static OversightCommandCenterSnapshot Empty { get; } = new();

    public IReadOnlyList<OversightEntityHealth> Entities { get; init; } = [];

    public int TotalUrgent { get; init; }

    public int TotalDropped { get; init; }

    public string? WorstEntityKey { get; init; }

    public string AttentionSummary { get; init; } = "All caught up.";
}
