using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Gathers this-week / last-week numbers from the analytics, response-time, and oversight services into a
/// <see cref="ReportInputs"/> the pure <see cref="BusinessReport"/> engine turns into insights + a document.
/// </summary>
public static class DashboardReportHelper
{
    public static ReportInputs GatherInputs(IReadOnlyList<MessengerInstance> instances)
    {
        var now = DateTimeOffset.Now;
        var weekStart = now.AddDays(-7);
        var priorStart = now.AddDays(-14);
        var sla = AppSettingsService.Instance.Settings.SlaThresholdMinutes;

        var analytics = MessageAnalyticsService.Instance;
        var tracker = ResponseTimeTracker.Instance;
        var snapshots = OversightChatSnapshotService.Instance;

        var wow = analytics.GetWeekOverWeek(instances);
        var (busyHour, busyDay) = analytics.GetBusiestWindow(instances);
        var thisWeek = tracker.GetStats(instances, weekStart, null, sla);
        var lastWeek = tracker.GetStats(instances, priorStart, weekStart, sla);

        // Per-account this-week message totals from the activity breakdown (customer-only, range-scoped).
        var breakdown = analytics.BuildActivityBreakdown(ActivityDimension.DayOfWeek, instances, weekStart, null);
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
            var frt = tracker.GetStats([instance], weekStart, null, sla);
            var messages = messagesByInstance.TryGetValue(instance.Id.Trim(), out var m) ? m : 0;

            accountLines.Add(new AccountReportLine(
                instance.DisplayName,
                messages,
                frt.MedianMinutes,
                frt.SampleCount,
                awaiting));
        }

        return new ReportInputs(
            PeriodLabel: $"{weekStart:MMM d} – {now:MMM d, yyyy}",
            MessagesThisWeek: wow.HasData ? wow.ThisWeekTotal : breakdown.Total,
            MessagesLastWeek: wow.HasData ? wow.LastWeekTotal : 0,
            MedianFrtThisWeekMinutes: thisWeek.MedianMinutes,
            FrtSamplesThisWeek: thisWeek.SampleCount,
            MedianFrtLastWeekMinutes: lastWeek.MedianMinutes,
            FrtSamplesLastWeek: lastWeek.SampleCount,
            SlaMetPercent: thisWeek.SlaCompliancePercent,
            SlaThresholdMinutes: sla,
            AnsweredThisWeek: thisWeek.SampleCount, // each measured reply in the window = one customer answered
            AwaitingNow: awaitingTotal,
            BusiestDay: busyDay,
            BusiestHour: busyHour,
            Accounts: accountLines);
    }
}
