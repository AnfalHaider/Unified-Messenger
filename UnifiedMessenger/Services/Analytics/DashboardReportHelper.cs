using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Gathers this-week / last-week numbers from the analytics, response-time, and oversight services into a
/// <see cref="ReportInputs"/> the pure <see cref="BusinessReport"/> engine turns into insights + a document.
/// </summary>
public static class DashboardReportHelper
{
    /// <summary>The report period presets, keyed by day-length; the noun feeds the report copy.</summary>
    public static readonly IReadOnlyList<(string Label, int Days, string Noun)> Ranges =
    [
        ("This week (7 days)", 7, "week"),
        ("Last 30 days", 30, "month"),
        ("Last 90 days", 90, "quarter"),
    ];

    public static ReportInputs GatherInputs(IReadOnlyList<MessengerInstance> instances, int periodDays = 7)
    {
        periodDays = Math.Clamp(periodDays, 1, 366);
        var noun = Ranges.FirstOrDefault(r => r.Days == periodDays).Noun ?? (periodDays <= 7 ? "week" : periodDays <= 31 ? "month" : "quarter");

        var now = DateTimeOffset.Now;
        var periodStart = now.AddDays(-periodDays);
        var priorStart = now.AddDays(-2 * periodDays);
        var sla = AppSettingsService.Instance.Settings.SlaThresholdMinutes;

        var analytics = MessageAnalyticsService.Instance;
        var tracker = ResponseTimeTracker.Instance;
        var snapshots = OversightChatSnapshotService.Instance;

        var (busyHour, busyDay) = analytics.GetBusiestWindow(instances);
        var contactInsight = ContactHistoryStore.Instance.GetInsight(
            instances.Where(i => !string.IsNullOrWhiteSpace(i.Id)).Select(i => i.Id),
            new DateTimeOffset(periodStart.UtcDateTime, TimeSpan.Zero),
            DateTimeOffset.UtcNow);
        var thisPeriod = tracker.GetStats(instances, periodStart, null, sla);
        var lastPeriod = tracker.GetStats(instances, priorStart, periodStart, sla);

        // Customer-message totals for the chosen period vs the prior equal-length period (range-scoped).
        var breakdown = analytics.BuildActivityBreakdown(ActivityDimension.DayOfWeek, instances, periodStart, null);
        var priorBreakdown = analytics.BuildActivityBreakdown(ActivityDimension.DayOfWeek, instances, priorStart, periodStart);
        var messagesByInstance = breakdown.Series.ToDictionary(s => s.InstanceId, s => s.Total, StringComparer.OrdinalIgnoreCase);

        var accountLines = new List<AccountReportLine>();
        var awaitingTotal = 0;
        foreach (var instance in instances)
        {
            if (string.IsNullOrWhiteSpace(instance.Id))
            {
                continue;
            }

            var awaiting = snapshots.GetAwaiting(instance.Id, null, null).Count;
            awaitingTotal += awaiting;
            var frt = tracker.GetStats([instance], periodStart, null, sla);
            var messages = messagesByInstance.TryGetValue(instance.Id.Trim(), out var m) ? m : 0;

            accountLines.Add(new AccountReportLine(
                instance.DisplayName,
                messages,
                frt.MedianMinutes,
                frt.SampleCount,
                awaiting));
        }

        return new ReportInputs(
            PeriodLabel: $"{periodStart:MMM d} – {now:MMM d, yyyy}",
            MessagesThisWeek: breakdown.Total,
            MessagesLastWeek: priorBreakdown.Total,
            MedianFrtThisWeekMinutes: thisPeriod.MedianMinutes,
            FrtSamplesThisWeek: thisPeriod.SampleCount,
            MedianFrtLastWeekMinutes: lastPeriod.MedianMinutes,
            FrtSamplesLastWeek: lastPeriod.SampleCount,
            SlaMetPercent: thisPeriod.SlaCompliancePercent,
            SlaThresholdMinutes: sla,
            AnsweredThisWeek: thisPeriod.SampleCount, // each measured reply in the window = one customer answered
            AwaitingNow: awaitingTotal,
            BusiestDay: busyDay,
            BusiestHour: busyHour,
            Accounts: accountLines,
            NewCustomersThisWeek: contactInsight.NewCount,
            ReturningCustomersThisWeek: contactInsight.ReturningCount,
            HasCustomerHistory: contactInsight.HasEnoughHistory,
            PeriodNoun: noun);
    }
}
