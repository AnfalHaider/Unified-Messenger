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
/// SLA outcomes split three ways for a donut. "No SLA" is real and must be shown separately: a channel we
/// cannot time (<c>SupportsResponseTiming == false</c>) neither met nor missed — printing it as either
/// would lie. Counts are threads/accounts, not percentages.
/// </summary>
public readonly record struct SlaBreakdown(int Met, int Missed, int NoSla)
{
    public int Total => Met + Missed + NoSla;

    public bool HasData => Total > 0;
}

/// <summary>
/// An account's place on the top-performers leaderboard: a 0–100 composite plus the inputs it was built
/// from, so the UI can show the score and explain it. Only accounts with real measured data are ranked —
/// an unsynced account is not "100%", it is unranked (<see cref="ChartSeriesBuilder.RankTopPerformers"/>).
/// </summary>
public readonly record struct TopPerformer(
    string Key,
    string DisplayName,
    int Score,
    int OnTimePercent,
    int AwaitingCount,
    int MeasuredCount);
