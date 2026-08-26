namespace UnifiedMessenger.Services;

/// <summary>
/// Which way a metric moved versus the prior period, and — separately — whether that move is good.
/// The two are not the same: response time falling is <see cref="DeltaDirection.Down"/> but good, while
/// message volume falling is also Down but not something to celebrate. The chart's delta badge colours by
/// <see cref="MetricDelta.IsFavourable"/>, never by direction alone.
/// </summary>
public enum DeltaDirection
{
    None,
    Up,
    Down
}

/// <summary>How to colour a delta: good, bad, or no judgement (a neutral-polarity metric like raw volume).</summary>
public enum DeltaSentiment
{
    Neutral,
    Favourable,
    Adverse
}

/// <summary>
/// A metric's change versus the prior equal-length period, ready for a delta badge: the signed percent,
/// the arrow direction, and the <see cref="DeltaSentiment"/> for <em>this</em> metric — so a volume drop
/// reads neutral while a response-time rise reads adverse, even though both are down-arrows.
/// </summary>
public readonly record struct MetricDelta(int Percent, DeltaDirection Direction, DeltaSentiment Sentiment, bool HasData)
{
    public static MetricDelta None { get; } = new(0, DeltaDirection.None, DeltaSentiment.Neutral, HasData: false);
}

/// <summary>Metric polarity: does a bigger number mean things are better or worse?</summary>
public enum MetricPolarity
{
    /// <summary>Higher is better — messages handled, SLA met %, replies within target.</summary>
    HigherIsBetter,

    /// <summary>Lower is better — response time, awaiting count, breaches.</summary>
    LowerIsBetter,

    /// <summary>Neither direction is inherently good or bad — raw volume.</summary>
    Neutral
}

/// <summary>One wedge of a donut: its label, colour, absolute value and share of the whole (0–100).</summary>
public readonly record struct DonutSlice(string Label, string ColorHex, int Value, int Percent);

/// <summary>
/// Thread counts split three ways for a donut: caught up (<c>Met</c>), behind (<c>Missed</c>), and not
/// measurable (<c>NoSla</c>). Counts are threads/accounts, not percentages.
/// </summary>
/// <remarks>
/// <b>The names say SLA; the numbers do not.</b> <c>Met</c>/<c>Missed</c> are apportioned by each entity's
/// <c>OnTimePercent</c>, which on the live chat-snapshot path is <c>caught-up</c> — the share of active
/// threads with unread cleared (<c>OversightRollupBuilder</c>) — and NOT first-response timing. Rendering
/// it as "SLA met" put a second, differently-computed SLA figure on the Analytics page beside the KPI
/// card, which really is FRT-based and uses a different denominator; they could disagree by any amount.
/// The user-facing labels now say caught up / behind / not measured. The field names are left alone
/// deliberately — renaming them reaches five files and the tests — so this remark is the warning:
/// <b>do not label anything built from this type "SLA".</b> <c>AnalyticsSlaLabellingTests</c> enforces it.
/// <para>
/// "Not measured" is real and must stay separate: a channel that cannot be timed
/// (<c>SupportsResponseTiming == false</c>) is neither caught up nor behind, and printing it as either
/// would lie.
/// </para>
/// </remarks>
public readonly record struct SlaBreakdown(int Met, int Missed, int NoSla)
{
    public int Total => Met + Missed + NoSla;

    public bool HasData => Total > 0;
}

/// <summary>
/// An account's place on the top-performers leaderboard. Every field is a real, nameable measurement —
/// there is deliberately no blended "score", so the number on screen is one the owner can act on. Only
/// accounts with real measured data are ranked — an unsynced account is not "100%", it is unranked
/// (<see cref="ChartSeriesBuilder.RankTopPerformers"/>).
/// </summary>
public readonly record struct TopPerformer(
    string Key,
    string DisplayName,
    int OnTimePercent,
    int AwaitingCount,
    int MeasuredCount);
