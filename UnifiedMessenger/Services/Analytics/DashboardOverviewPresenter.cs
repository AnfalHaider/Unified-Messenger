using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>The three mockup overview cards' data, gathered once.</summary>
public sealed class DashboardOverview
{
    /// <summary>Message volume by weekday over the range (bar chart).</summary>
    public IReadOnlyList<(string Label, double Value)> MessagesByDay { get; init; } = [];

    /// <summary>Best-performing accounts, best first (already gated to accounts with real data).</summary>
    public IReadOnlyList<TopPerformer> TopPerformers { get; init; } = [];

    /// <summary>Share of messages per account — the honest "channel distribution".</summary>
    public IReadOnlyList<DonutSlice> ChannelShare { get; init; } = [];

    public int TotalMessages { get; init; }
}

/// <summary>
/// Gathers the dashboard's overview row. Like the analytics presenter, all real arithmetic is delegated
/// to the unit-tested <see cref="ChartSeriesBuilder"/>.
/// </summary>
public static class DashboardOverviewPresenter
{
    public static DashboardOverview Build(IReadOnlyList<MessengerInstance> instances, int rangeDays = 7)
    {
        rangeDays = Math.Max(1, rangeDays);
        var now = DateTimeOffset.Now;
        var from = now.AddDays(-rangeDays);

        var patterns = MessageAnalyticsService.Instance
            .BuildActivityPatterns(ActivityDimension.DayOfWeek, instances, from, now);

        // Per-account share. NOTE: this is deliberately per ACCOUNT, not per platform — Google Business
        // has no message channel (its Messages product shut down in 2024), so a "62% WhatsApp / 28% Google"
        // split of *messages* would be fiction. Accounts are the honest unit.
        var breakdown = MessageAnalyticsService.Instance
            .BuildActivityBreakdown(ActivityDimension.DayOfWeek, instances, from, now);
        var colors = ChartPalette.ResolveSeriesColors(breakdown.Series);
        var shareRows = breakdown.Series
            .Select(s => (
                Label: s.DisplayName,
                ColorHex: colors.TryGetValue(s.InstanceId, out var hex) ? hex : s.AccentColor,
                Value: s.Total))
            .ToList();

        var entities = OversightService.Instance
            .BuildSnapshot(OversightGrouping.ByInstance, instances)
            .Entities;

        return new DashboardOverview
        {
            MessagesByDay = patterns.Labels.Select((label, i) => (label, (double)patterns.Values[i])).ToList(),
            TopPerformers = ChartSeriesBuilder.RankTopPerformers(entities),
            ChannelShare = ChartSeriesBuilder.BuildShareSlices(shareRows),
            TotalMessages = patterns.Total
        };
    }
}
