using System.Globalization;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Pure shaping of the numbers the chart controls need but the underlying stores don't already produce:
/// vs-prior-period deltas, donut shares that sum to exactly 100, the three-way SLA split, a top-performers
/// score, and zero-padded daily series. Every method here is deterministic and free of the singleton
/// stores, so it can be unit-tested directly — the stores call in with plain inputs.
/// </summary>
public static class ChartSeriesBuilder
{
    /// <summary>
    /// The change from <paramref name="previous"/> to <paramref name="current"/>, coloured by whether the
    /// move is good for a metric of the given <paramref name="polarity"/> rather than by its sign. Returns
    /// <see cref="MetricDelta.None"/> when there is no prior period to compare against (a first-week metric
    /// has no honest delta).
    /// </summary>
    public static MetricDelta ComputeDelta(double current, double previous, MetricPolarity polarity)
    {
        if (previous <= 0)
        {
            // No baseline: 0 → something isn't a "percent change", and dividing by zero is a lie.
            return MetricDelta.None;
        }

        var raw = (current - previous) / previous * 100.0;
        var percent = (int)Math.Round(Math.Abs(raw), MidpointRounding.AwayFromZero);
        var direction = raw > 0 ? DeltaDirection.Up : raw < 0 ? DeltaDirection.Down : DeltaDirection.None;

        var sentiment = polarity switch
        {
            MetricPolarity.Neutral => DeltaSentiment.Neutral,
            _ when direction == DeltaDirection.None => DeltaSentiment.Neutral,
            MetricPolarity.HigherIsBetter => raw > 0 ? DeltaSentiment.Favourable : DeltaSentiment.Adverse,
            MetricPolarity.LowerIsBetter => raw < 0 ? DeltaSentiment.Favourable : DeltaSentiment.Adverse,
            _ => DeltaSentiment.Neutral
        };

        return new MetricDelta(percent, direction, sentiment, HasData: true);
    }

    /// <summary>
    /// Turns raw (label, colour, value) rows into donut slices whose percentages sum to <b>exactly</b> 100
    /// (largest-remainder rounding), dropping zero-value rows so an empty wedge can't appear. Returns an
    /// empty list when everything is zero, so the caller shows an empty state rather than a full ring of
    /// nothing.
    /// </summary>
    public static IReadOnlyList<DonutSlice> BuildShareSlices(
        IReadOnlyList<(string Label, string ColorHex, int Value)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var nonZero = rows.Where(r => r.Value > 0).ToList();
        var total = nonZero.Sum(r => r.Value);
        if (total <= 0)
        {
            return [];
        }

        // Floor each share, then hand the leftover points to the rows with the largest fractional part —
        // the standard way to make rounded percentages total 100 instead of 99 or 101.
        var scored = nonZero
            .Select(r =>
            {
                var exact = r.Value * 100.0 / total;
                var floor = (int)Math.Floor(exact);
                return (r.Label, r.ColorHex, r.Value, Floor: floor, Frac: exact - floor);
            })
            .ToList();

        var remaining = 100 - scored.Sum(s => s.Floor);
        foreach (var idx in scored
                     .Select((s, i) => (i, s.Frac))
                     .OrderByDescending(x => x.Frac)
                     .Take(Math.Max(0, remaining))
                     .Select(x => x.i))
        {
            var s = scored[idx];
            scored[idx] = (s.Label, s.ColorHex, s.Value, s.Floor + 1, s.Frac);
        }

        return scored
            .Select(s => new DonutSlice(s.Label, s.ColorHex, s.Value, s.Floor))
            .ToList();
    }

    /// <summary>
    /// The three-way SLA split across a set of oversight entities. An entity that can't be timed
    /// (<see cref="OversightEntityHealth.SupportsResponseTiming"/> false, or no measured threads) counts
    /// entirely as "no SLA" — it never inflates met or missed. For a measurable entity, its measured
    /// threads are apportioned met/missed by its on-time %.
    /// </summary>
    public static SlaBreakdown BuildSlaBreakdown(IEnumerable<OversightEntityHealth> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var met = 0;
        var missed = 0;
        var noSla = 0;

        foreach (var e in entities)
        {
            if (!e.SupportsResponseTiming || e.MeasuredCount <= 0 || !e.HasChatData)
            {
                noSla += Math.Max(e.MeasuredCount, e.AccountCount);
                continue;
            }

            var metHere = (int)Math.Round(e.MeasuredCount * Math.Clamp(e.OnTimePercent, 0, 100) / 100.0,
                MidpointRounding.AwayFromZero);
            met += metHere;
            missed += Math.Max(0, e.MeasuredCount - metHere);
        }

        return new SlaBreakdown(met, missed, noSla);
    }

    /// <summary>
    /// Ranks accounts for a "top performing" leaderboard, best first. This deliberately inverts the
    /// worst-first oversight ordering, but not naively: <see cref="OversightEntityHealth.OnTimePercent"/>
    /// defaults to 100 for an account with no measured data, so a raw inversion would crown the accounts we
    /// know nothing about. Only entities with real, timeable data are ranked; the rest are omitted.
    /// </summary>
    /// <remarks>
    /// Score (0–100) = on-time % penalised by current backlog: <c>onTime − min(20, awaiting·2)</c>. Backlog
    /// caps its penalty so a fast-replying busy account isn't buried, and the cap is documented rather than
    /// magic. Ties break on more measured data (a track record beats a single lucky sample).
    /// </remarks>
    public static IReadOnlyList<TopPerformer> RankTopPerformers(
        IEnumerable<OversightEntityHealth> entities,
        int max = 5)
    {
        ArgumentNullException.ThrowIfNull(entities);

        return entities
            .Where(e => e.SupportsResponseTiming && e.MeasuredCount > 0 && e.HasChatData)
            .Select(e =>
            {
                var backlogPenalty = Math.Min(20, Math.Max(0, e.AwaitingCount) * 2);
                var score = Math.Clamp(Math.Clamp(e.OnTimePercent, 0, 100) - backlogPenalty, 0, 100);
                return new TopPerformer(e.Key, e.DisplayName, score, e.OnTimePercent, e.AwaitingCount, e.MeasuredCount);
            })
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.MeasuredCount)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, max))
            .ToList();
    }

    /// <summary>
    /// A zero-padded daily series of length <paramref name="days"/>, oldest→newest, ending on
    /// <paramref name="today"/>. Unlike a store that omits empty days, this is index-aligned to dates — the
    /// only shape that can be sliced into "last 7" vs "previous 7" for a week-over-week comparison.
    /// </summary>
    public static IReadOnlyList<int> BuildZeroPaddedDailySeries(
        IReadOnlyDictionary<string, int> byDayKey,
        DateTime today,
        int days)
    {
        ArgumentNullException.ThrowIfNull(byDayKey);

        var result = new int[Math.Max(0, days)];
        for (var i = 0; i < result.Length; i++)
        {
            var key = today.Date.AddDays(-(result.Length - 1 - i)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            result[i] = byDayKey.TryGetValue(key, out var v) ? v : 0;
        }

        return result;
    }

    /// <summary>Formats a count for a chart axis: 0–999 as-is, then "1.2K", "15K", "1.1M".</summary>
    public static string FormatAxisCount(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000)
        {
            return (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (abs >= 1_000)
        {
            return (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
    }
}
