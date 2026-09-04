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
    All,
    Custom
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

    /// <summary>Exact number of chats awaiting a reply (unread &gt; 0) within the window — customers not yet responded to.</summary>
    public int AwaitingCount { get; init; }

    /// <summary>
    /// True when we have WhatsApp's unread chat data for this entity. False means the chat-store read
    /// hasn't landed yet (e.g. the account's WhatsApp Web is still loading) — the UI shows "syncing…"
    /// rather than stale thread-based numbers that the awaiting list can't back up.
    /// </summary>
    public bool HasChatData { get; init; } = true;

    /// <summary>Share of LIVE actionable threads replied within (or still inside) the SLA, 0–100.</summary>
    public int OnTimePercent { get; init; } = 100;

    /// <summary>
    /// True when at least one member channel can actually supply reply-timing data
    /// (<see cref="PlatformCapabilities.SupportsFrt"/>). When false, <see cref="OnTimePercent"/> carries no
    /// information and the UI must say so rather than print a flattering 100% — a channel we cannot measure
    /// is not a channel that succeeded. Latency-incapable channels are excluded from the on-time
    /// denominator entirely, so they can neither inflate nor deflate the number.
    /// </summary>
    public bool SupportsResponseTiming { get; init; } = true;

    public int UrgentCount { get; init; }

    public int DroppedCount { get; init; }

    /// <summary>
    /// Open in-window threads whose business-hours reply-latency SLA has breached — the MASTER-PLAN §8
    /// "on-time" signal (reply timing within each location's working hours), surfaced alongside the
    /// unread-based caught-up %. Derived from live threads + per-location business hours via
    /// <see cref="ThreadData.IsSlaBreached"/>; 0 when there is no thread data yet.
    /// </summary>
    public int SlaBreachedCount { get; init; }

    public bool IsStale { get; init; }

    /// <summary>
    /// True when every member account of this entity is showing a sign-in screen, so nothing has been
    /// read and no figure on it is a measurement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately separate from <see cref="IsStale"/>, which it would otherwise be folded into —
    /// not-connected already sets stale. The two demand opposite treatments. Stale means the app has real
    /// numbers that are getting old, so it shows them and says when they were read. Signed out means the
    /// app has nothing, so it must show <b>no figures at all</b>: a zero is a measurement, and rendering
    /// one here would state, in the product's own voice, that nobody is waiting on a channel it cannot
    /// see. That is the failure <see cref="UnifiedMessenger.Services.SignInGate"/> exists to prevent, and
    /// collapsing this flag back into <see cref="IsStale"/> reintroduces it.
    /// </para>
    /// <para>
    /// Requires <i>every</i> member to be signed out, because a location rolling up three accounts of
    /// which one still reads has genuine data — it is partial, not absent, and partial coverage is
    /// reported by <see cref="UnifiedMessenger.Services.ChannelCoverage"/> instead.
    /// </para>
    /// </remarks>
    public bool IsSignedOut { get; init; }

    /// <summary>
    /// True when the most recent read of at least one member account failed outright, so this entity's
    /// numbers are missing data rather than reporting a quiet period.
    /// </summary>
    /// <remarks>
    /// Set only from a <see cref="UnifiedMessenger.Services.AccountReadHealth"/> record of an actual
    /// failure — never inferred from a zero count, because a genuinely quiet account must not be accused
    /// of being broken. Distinct from <see cref="IsStale"/>: stale means "the data we have is old",
    /// this means "we could not get data at all".
    /// </remarks>
    public bool ReadFailed { get; init; }

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
