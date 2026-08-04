using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>One KPI tile's rendered value plus its change versus the prior equal-length period.</summary>
public readonly record struct AnalyticsKpi(string Value, MetricDelta Delta);

/// <summary>Everything the Analytics page renders, gathered once so the page stays a thin view.</summary>
public sealed class AnalyticsView
{
    public AnalyticsKpi Messages { get; init; }
    public AnalyticsKpi ResponseTime { get; init; }
    public AnalyticsKpi Replies15 { get; init; }
    public AnalyticsKpi SlaMet { get; init; }

    /// <summary>Message volume by weekday over the range (bar chart).</summary>
    public IReadOnlyList<(string Label, double Value)> MessagesByDay { get; init; } = [];

    /// <summary>Median first-reply minutes per day (area chart) with its day labels.</summary>
    public IReadOnlyList<double> ResponseDaily { get; init; } = [];
    public IReadOnlyList<string> ResponseLabels { get; init; } = [];

    /// <summary>Percentage of replies within 15 minutes per day (area chart) with its day labels.</summary>
    public IReadOnlyList<double> Replies15Daily { get; init; } = [];
    public IReadOnlyList<string> Replies15Labels { get; init; } = [];

    /// <summary>SLA met / missed / no-SLA across accounts (donut).</summary>
    public SlaBreakdown Sla { get; init; }
}

/// <summary>
/// Gathers the Analytics page's numbers from the live services in one place. Not pure — it reads the
/// singleton stores — but every non-trivial calculation it performs (deltas, the SLA split) is delegated
/// to the unit-tested <see cref="ChartSeriesBuilder"/>, so the risk here is wiring, not arithmetic.
/// </summary>
public static class AnalyticsPagePresenter
{
    private const int Replies15ThresholdMinutes = 15;

    public static AnalyticsView Build(IReadOnlyList<MessengerInstance> instances, int rangeDays)
    {
        rangeDays = Math.Max(1, rangeDays);
        var now = DateTimeOffset.Now;
        var periodStart = now.AddDays(-rangeDays);
        var priorStart = now.AddDays(-2 * rangeDays);
        var slaSetting = AppSettingsService.Instance.Settings.SlaThresholdMinutes;

        // ── Messages: total inbound in range, vs the prior equal window (raw volume → neutral polarity) ──
        var messagesNow = MessageAnalyticsService.Instance
            .BuildActivityPatterns(ActivityDimension.DayOfWeek, instances, periodStart, now);
        var messagesPrior = MessageAnalyticsService.Instance
            .BuildActivityPatterns(ActivityDimension.DayOfWeek, instances, priorStart, periodStart);
        var messagesDelta = ChartSeriesBuilder.ComputeDelta(messagesNow.Total, messagesPrior.Total, MetricPolarity.Neutral);

        // ── Response time / replies / SLA from the response tracker, this vs prior window ──
        var statNow = ResponseTimeTracker.Instance.GetStats(instances, periodStart, null, slaSetting);
        var statPrior = ResponseTimeTracker.Instance.GetStats(instances, priorStart, periodStart, slaSetting);
        var replyNow = ResponseTimeTracker.Instance.GetStats(instances, periodStart, null, Replies15ThresholdMinutes);
        var replyPrior = ResponseTimeTracker.Instance.GetStats(instances, priorStart, periodStart, Replies15ThresholdMinutes);

        var responseDelta = ChartSeriesBuilder.ComputeDelta(statNow.MedianMinutes, statPrior.MedianMinutes, MetricPolarity.LowerIsBetter);
        var repliesDelta = ChartSeriesBuilder.ComputeDelta(replyNow.SlaCompliancePercent, replyPrior.SlaCompliancePercent, MetricPolarity.HigherIsBetter);
        var slaDelta = ChartSeriesBuilder.ComputeDelta(statNow.SlaCompliancePercent, statPrior.SlaCompliancePercent, MetricPolarity.HigherIsBetter);

        // ── Daily series for the two area charts ──
        var days = Math.Min(rangeDays, 31);
        var medians = ResponseTimeTracker.Instance.GetDailyMedians(instances, days);
        var within = ResponseTimeTracker.Instance.GetDailyWithinThreshold(instances, Replies15ThresholdMinutes, days);

        // ── SLA breakdown across accounts (three-way, capability-aware) ──
        var entities = OversightService.Instance
            .BuildSnapshot(OversightGrouping.ByInstance, instances)
            .Entities;
        var slaBreakdown = ChartSeriesBuilder.BuildSlaBreakdown(entities);

        return new AnalyticsView
        {
            Messages = new AnalyticsKpi(ChartSeriesBuilder.FormatAxisCount(messagesNow.Total), messagesDelta),
            ResponseTime = new AnalyticsKpi(statNow.HasData ? BusinessReport.FormatMinutes(statNow.MedianMinutes) : "—", responseDelta),
            Replies15 = new AnalyticsKpi(replyNow.HasData ? $"{replyNow.SlaCompliancePercent}%" : "—", repliesDelta),
            SlaMet = new AnalyticsKpi(statNow.HasData ? $"{statNow.SlaCompliancePercent}%" : "—", slaDelta),
            MessagesByDay = messagesNow.Labels
                .Select((label, i) => (label, (double)messagesNow.Values[i]))
                .ToList(),
            ResponseDaily = medians.Select(p => p.MedianMinutes).ToList(),
            ResponseLabels = medians.Select(p => p.Label).ToList(),
            Replies15Daily = within.Select(p => (double)p.Percent).ToList(),
            Replies15Labels = within.Select(p => p.Label).ToList(),
            Sla = slaBreakdown
        };
    }
}
