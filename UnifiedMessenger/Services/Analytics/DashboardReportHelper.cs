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

    /// <param name="scopeLabel">
    /// The branch this report covers, when it covers only one. It is folded into
    /// <see cref="ReportInputs.PeriodLabel"/> rather than carried separately because that label is what
    /// reaches the Markdown export (<c>BusinessReport</c> writes <c>"# Business report — {PeriodLabel}"</c>).
    /// A single-branch report saved to a file that does not say which branch it covers stops being a report
    /// and becomes a wrong document the moment it leaves the app.
    /// </param>
    public static ReportInputs GatherInputs(
        IReadOnlyList<MessengerInstance> instances,
        int periodDays = 7,
        string? scopeLabel = null)
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

        // Scoped to the period the report is about. Unscoped, this printed an all-time busiest day inside a
        // document headed "this week".
        var (busyHour, busyDay) = analytics.GetBusiestWindow(instances, periodStart, DateTimeOffset.UtcNow);
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
            PeriodLabel: string.IsNullOrWhiteSpace(scopeLabel)
                ? $"{periodStart:MMM d} – {now:MMM d, yyyy}"
                : $"{periodStart:MMM d} – {now:MMM d, yyyy} · {scopeLabel.Trim()}",
            MessagesThisWeek: breakdown.Total,
            MessagesLastWeek: priorBreakdown.Total,
            MedianFrtThisWeekMinutes: thisPeriod.MedianMinutes,
            FrtSamplesThisWeek: thisPeriod.SampleCount,
            MedianFrtLastWeekMinutes: lastPeriod.MedianMinutes,
            FrtSamplesLastWeek: lastPeriod.SampleCount,
            SlaMetPercent: thisPeriod.SlaCompliancePercent,
            SlaThresholdMinutes: sla,
            // Deliberately the same figure as FrtSamplesThisWeek — because it IS that figure. The report
            // used to print it once as "N replies measured" and again as "replied to N waiting customers",
            // two incompatible nouns for one number: a customer who enquires three times in the period
            // produces three first-reply samples, not three customers. The wording now says
            // "conversations", which is what the tracker actually counts.
            AnsweredThisWeek: thisPeriod.SampleCount,
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
