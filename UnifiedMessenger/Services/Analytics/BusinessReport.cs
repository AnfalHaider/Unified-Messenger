using System.Globalization;
using System.Text;

namespace UnifiedMessenger.Services;

/// <summary>How notable/urgent an insight is — drives colour + ordering.</summary>
public enum InsightSeverity
{
    Good,
    Info,
    Warn,
}

/// <summary>One plain-language observation about the business's messaging performance.</summary>
public sealed record BusinessInsight(InsightSeverity Severity, string Title, string Detail);

/// <summary>One account's line in the report — its share of volume, reply speed, and current backlog.</summary>
public sealed record AccountReportLine(
    string DisplayName,
    int Messages,
    double MedianFrtMinutes,
    int FrtSamples,
    int AwaitingNow,
    // The channel this account is on. Without it, a "—" in the reply-time column reads as broken rather
    // than as not-measurable, which is a different thing and the only one of the two worth acting on.
    string Channel = "");

/// <summary>
/// All the numbers a <see cref="BusinessReport"/> needs, gathered from the analytics / response / oversight
/// services by the caller. Kept as a plain input record so the report logic is pure and unit-testable.
/// </summary>
public sealed record ReportInputs(
    string PeriodLabel,
    int MessagesThisWeek,
    int MessagesLastWeek,
    double MedianFrtThisWeekMinutes,
    int FrtSamplesThisWeek,
    double MedianFrtLastWeekMinutes,
    int FrtSamplesLastWeek,
    int SlaMetPercent,
    int SlaThresholdMinutes,
    int AnsweredThisWeek,
    int AwaitingNow,
    string BusiestDay,
    string BusiestHour,
    IReadOnlyList<AccountReportLine> Accounts,
    // New-vs-returning customers (optional — populated once ≥1 week of contact history has accrued).
    int NewCustomersThisWeek = 0,
    int ReturningCustomersThisWeek = 0,
    bool HasCustomerHistory = false,
    // The period this report covers, as a noun for the copy ("week", "month", "quarter"). Default "week".
    string PeriodNoun = "week",
    // One sentence naming which accounts these figures cover — see ChannelScope. A saved .md outlives the
    // screen that explained it, so an exported document that does not state its own scope becomes a wrong
    // document the moment it is forwarded to anyone.
    string ChannelScopeLine = "");

/// <summary>The built report: ranked insights, a copy-ready plain-language summary, and a markdown document.</summary>
public sealed record BusinessReportResult(
    IReadOnlyList<BusinessInsight> Insights,
    string Summary,
    string Markdown);

/// <summary>
/// Turns a week's aggregate numbers into ranked, plain-language insights and a shareable report — anomaly
/// detection (response-time degradation, rising backlog, quiet accounts), comparative call-outs (busiest /
/// slowest account), and SLA/volume trends. Pure and deterministic; the dashboard may additionally narrate
/// the <see cref="BusinessReportResult.Summary"/> via local AI, but this always stands alone.
/// </summary>
public static class BusinessReport
{
    // Response time is "degraded" when this week's median is both meaningfully higher than last week and
    // above a floor (so 1→3 min doesn't cry wolf), or simply over the SLA target.
    private const double SlowerFactor = 1.5;
    private const double SlowerFloorMinutes = 10;

    public static BusinessReportResult Build(ReportInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // The period word ("week"/"month"/"quarter") so the copy reads naturally for any chosen range.
        var noun = string.IsNullOrWhiteSpace(input.PeriodNoun) ? "week" : input.PeriodNoun;
        var thisPeriod = $"this {noun}";
        var priorPeriod = $"last {noun}";

        var insights = new List<BusinessInsight>();

        // ── Volume trend ────────────────────────────────────────────────────────────────────
        if (input.MessagesThisWeek > 0 || input.MessagesLastWeek > 0)
        {
            if (input.MessagesLastWeek == 0)
            {
                insights.Add(new BusinessInsight(InsightSeverity.Info,
                    $"First {noun} of tracked activity",
                    $"{input.MessagesThisWeek} customer messages {thisPeriod} — next {noun} you'll get a period-over-period trend."));
            }
            else
            {
                var deltaPct = (int)Math.Round((input.MessagesThisWeek - input.MessagesLastWeek) * 100.0 / input.MessagesLastWeek);
                if (Math.Abs(deltaPct) >= 15)
                {
                    var dir = deltaPct > 0 ? "up" : "down";
                    insights.Add(new BusinessInsight(deltaPct > 0 ? InsightSeverity.Info : InsightSeverity.Warn,
                        $"Message volume is {dir} {Math.Abs(deltaPct)}% {thisPeriod}",
                        $"{input.MessagesThisWeek} messages vs {input.MessagesLastWeek} {priorPeriod}."
                        + (deltaPct > 0 ? " Make sure coverage keeps up." : " Quieter than usual.")));
                }
                else
                {
                    insights.Add(new BusinessInsight(InsightSeverity.Good,
                        "Volume steady",
                        $"{input.MessagesThisWeek} messages {thisPeriod}, about the same as {priorPeriod}."));
                }
            }
        }

        // ── Response-time trend (anomaly) ───────────────────────────────────────────────────
        if (input.FrtSamplesThisWeek > 0)
        {
            var thisWk = input.MedianFrtThisWeekMinutes;
            var lastWk = input.MedianFrtLastWeekMinutes;
            var slowerThanLastWeek = input.FrtSamplesLastWeek > 0 && lastWk > 0
                && thisWk >= lastWk * SlowerFactor && thisWk >= SlowerFloorMinutes;
            var overSla = input.SlaThresholdMinutes > 0 && thisWk > input.SlaThresholdMinutes;

            if (slowerThanLastWeek)
            {
                insights.Add(new BusinessInsight(InsightSeverity.Warn,
                    $"Replies are slower {thisPeriod}",
                    $"Median first reply is {FormatMinutes(thisWk)} — up from {FormatMinutes(lastWk)} {priorPeriod}."));
            }
            else if (overSla)
            {
                insights.Add(new BusinessInsight(InsightSeverity.Warn,
                    "Reply speed is over your target",
                    $"Median first reply is {FormatMinutes(thisWk)} vs your {input.SlaThresholdMinutes}-minute target."));
            }
            else
            {
                // SURVIVORSHIP. The median is computed over conversations that GOT a reply; the ones still
                // waiting have no first-response time yet and are absent from it by construction. So the
                // report could say "Reply speed is healthy — median 1 min across 29 replies" six rows below
                // "103 customers waiting on a reply right now", and mean both. The verdict was being read
                // off the survivors and presented as the state of the business.
                //
                // Where more customers are waiting than were answered, the median describes too small a
                // slice of the week to carry a healthy verdict — so it keeps the measurement and drops the
                // claim, and names the population it actually covers.
                var answered = input.FrtSamplesThisWeek;
                var outnumbered = input.AwaitingNow > answered;

                insights.Add(outnumbered
                    ? new BusinessInsight(InsightSeverity.Info,
                        $"Reply speed looks good for the {answered} that were answered",
                        $"Median first reply is {FormatMinutes(thisWk)} across {answered} "
                        + $"{(answered == 1 ? "reply" : "replies")} — but {input.AwaitingNow} "
                        + $"{(input.AwaitingNow == 1 ? "customer is" : "customers are")} still waiting and "
                        + "are not in that figure.")
                    : new BusinessInsight(InsightSeverity.Good,
                        "Reply speed is healthy",
                        $"Median first reply is {FormatMinutes(thisWk)} across {answered} "
                        + $"{(answered == 1 ? "reply" : "replies")}."));
            }
        }

        // ── SLA compliance ──────────────────────────────────────────────────────────────────
        if (input.FrtSamplesThisWeek > 0)
        {
            var sev = input.SlaMetPercent >= 90 ? InsightSeverity.Good
                : input.SlaMetPercent >= 70 ? InsightSeverity.Info
                : InsightSeverity.Warn;
            insights.Add(new BusinessInsight(sev,
                $"{input.SlaMetPercent}% of replies met your {input.SlaThresholdMinutes}-min target",
                input.SlaMetPercent >= 90 ? "Great responsiveness." : "Room to speed up first replies."));
        }

        // ── Current backlog ─────────────────────────────────────────────────────────────────
        if (input.AwaitingNow > 0)
        {
            insights.Add(new BusinessInsight(input.AwaitingNow >= 10 ? InsightSeverity.Warn : InsightSeverity.Info,
                $"{input.AwaitingNow} customer{(input.AwaitingNow == 1 ? "" : "s")} waiting on a reply right now",
                "Open the Needs-reply list to clear them, most urgent first."));
        }
        else if (input.MessagesThisWeek > 0)
        {
            insights.Add(new BusinessInsight(InsightSeverity.Good,
                "All caught up",
                "No customers are currently waiting on a reply."));
        }

        // ── New vs returning customers (needs ≥1 week of contact history) ───────────────────
        if (input.HasCustomerHistory && (input.NewCustomersThisWeek + input.ReturningCustomersThisWeek) > 0)
        {
            var activeCustomers = input.NewCustomersThisWeek + input.ReturningCustomersThisWeek;
            // Shares are bounded 0-100, so they use the honest rule: rounding 997/1000 up to 100 here would
            // produce a sentence that contradicts itself — "100% ... had contacted you before; 3 reached
            // out for the first time."
            var returnRate = MetricMath.HonestPercent(input.ReturningCustomersThisWeek, activeCustomers);
            insights.Add(new BusinessInsight(InsightSeverity.Info,
                $"{input.NewCustomersThisWeek} new · {input.ReturningCustomersThisWeek} returning customers {thisPeriod}",
                $"{returnRate}% of the {activeCustomers} customers who messaged {thisPeriod} had contacted you before"
                + (input.NewCustomersThisWeek > 0
                    ? $"; {input.NewCustomersThisWeek} reached out for the first time."
                    : ".")));
        }

        // ── Comparative call-outs across accounts ───────────────────────────────────────────
        var active = input.Accounts.Where(a => a.Messages > 0).ToList();
        if (active.Count > 1)
        {
            var top = active.OrderByDescending(a => a.Messages).First();
            // This call-out only fires when 2+ accounts are active, so a rounded-up 100% would claim one
            // account is all of the volume in the same breath as naming it "busiest" among several.
            var share = MetricMath.HonestPercent(top.Messages, Math.Max(1, active.Sum(a => a.Messages)));
            insights.Add(new BusinessInsight(InsightSeverity.Info,
                $"{top.DisplayName} is your busiest account",
                $"{top.Messages} messages this {noun} — {share}% of all customer volume."));

            var slowest = active.Where(a => a.FrtSamples > 0).OrderByDescending(a => a.MedianFrtMinutes).FirstOrDefault();
            if (slowest is not null && input.SlaThresholdMinutes > 0 && slowest.MedianFrtMinutes > input.SlaThresholdMinutes)
            {
                insights.Add(new BusinessInsight(InsightSeverity.Warn,
                    $"{slowest.DisplayName} has the slowest replies",
                    $"Median {FormatMinutes(slowest.MedianFrtMinutes)} — over your {input.SlaThresholdMinutes}-min target."));
            }
        }

        // Quiet-account anomaly: an account with a current backlog but no measured replies is being neglected.
        foreach (var a in input.Accounts.Where(a => a.AwaitingNow >= 3 && a.FrtSamples == 0))
        {
            insights.Add(new BusinessInsight(InsightSeverity.Warn,
                $"{a.DisplayName} may be neglected",
                $"{a.AwaitingNow} waiting and no replies measured yet this {noun}."));
        }

        // Warn first, then info, then good — most-actionable at the top.
        var ranked = insights
            .OrderBy(i => i.Severity switch { InsightSeverity.Warn => 0, InsightSeverity.Info => 1, _ => 2 })
            .ToList();

        return new BusinessReportResult(ranked, BuildSummary(input, ranked), BuildMarkdown(input, ranked));
    }

    private static string BuildSummary(ReportInputs input, IReadOnlyList<BusinessInsight> insights)
    {
        var noun = string.IsNullOrWhiteSpace(input.PeriodNoun) ? "week" : input.PeriodNoun;
        var warns = insights.Where(i => i.Severity == InsightSeverity.Warn).Take(2).ToList();
        if (warns.Count > 0)
        {
            // Titles are kept verbatim. Lower-casing them read tidily until an insight title carried an
            // account name — "Focus this week: depilex dha-2 whatsapp may be neglected" mangles the
            // owner's own branch naming in the single most prominent sentence of the report.
            return $"Focus this {noun}: " + string.Join("; ", warns.Select(w => w.Title)) + ".";
        }

        if (input.MessagesThisWeek == 0)
        {
            return $"No customer activity recorded this {noun} yet.";
        }

        return $"A solid {noun} — {input.MessagesThisWeek} messages handled, "
            + (input.FrtSamplesThisWeek > 0 ? $"median reply {FormatMinutes(input.MedianFrtThisWeekMinutes)}, " : string.Empty)
            + $"{input.AwaitingNow} waiting now.";
    }

    private static string BuildMarkdown(ReportInputs input, IReadOnlyList<BusinessInsight> insights)
    {
        var noun = string.IsNullOrWhiteSpace(input.PeriodNoun) ? "week" : input.PeriodNoun;
        var sb = new StringBuilder();
        sb.Append("# Business report — ").AppendLine(input.PeriodLabel);
        if (!string.IsNullOrWhiteSpace(input.ChannelScopeLine))
        {
            sb.AppendLine();
            sb.Append('_').Append(input.ChannelScopeLine.Trim()).AppendLine("_");
        }

        sb.AppendLine();
        sb.AppendLine("## At a glance");
        sb.Append("- Customer messages this ").Append(noun).Append(": **").Append(input.MessagesThisWeek).Append("**");
        if (input.MessagesLastWeek > 0)
        {
            var d = input.MessagesThisWeek - input.MessagesLastWeek;
            sb.Append(" (").Append(d >= 0 ? "+" : "").Append(d).Append(" vs last ").Append(noun).Append(")");
        }

        sb.AppendLine();
        if (input.FrtSamplesThisWeek > 0)
        {
            sb.Append("- Median first reply: **").Append(FormatMinutes(input.MedianFrtThisWeekMinutes))
                .Append("** (").Append(input.FrtSamplesThisWeek).AppendLine(" replies measured)");
            sb.Append("- Replies within your ").Append(input.SlaThresholdMinutes).Append("-min target: **")
                .Append(input.SlaMetPercent).AppendLine("%**");
            sb.Append("- Replied to **").Append(input.AnsweredThisWeek).Append("** waiting conversations this ").AppendLine(noun);
        }

        sb.Append("- Waiting on a reply right now: **").Append(input.AwaitingNow).AppendLine("**");
        if (input.HasCustomerHistory && (input.NewCustomersThisWeek + input.ReturningCustomersThisWeek) > 0)
        {
            sb.Append("- Customers this ").Append(noun).Append(": **").Append(input.NewCustomersThisWeek).Append("** new, **")
                .Append(input.ReturningCustomersThisWeek).AppendLine("** returning");
        }

        if (!string.IsNullOrWhiteSpace(input.BusiestDay) && input.BusiestDay != "—")
        {
            sb.Append("- Busiest: **").Append(input.BusiestDay).Append("**, around **").Append(input.BusiestHour).AppendLine("**");
        }

        sb.AppendLine();
        sb.AppendLine("## What to focus on");
        if (insights.Count == 0)
        {
            sb.AppendLine("- Nothing notable — activity is steady.");
        }
        else
        {
            foreach (var i in insights)
            {
                var mark = i.Severity switch { InsightSeverity.Warn => "⚠", InsightSeverity.Good => "✓", _ => "•" };
                sb.Append(mark).Append(' ').Append("**").Append(i.Title).Append("** — ").AppendLine(i.Detail);
            }
        }

        var active = input.Accounts.Where(a => a.Messages > 0 || a.AwaitingNow > 0).ToList();
        if (active.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## By account");
            sb.AppendLine();
            sb.AppendLine("| Account | Channel | Messages | Median reply | Waiting now |");
            sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (var a in active.OrderByDescending(a => a.Messages))
            {
                var frt = a.FrtSamples > 0 ? FormatMinutes(a.MedianFrtMinutes) : "—";
                var channel = string.IsNullOrWhiteSpace(a.Channel) ? "—" : a.Channel;
                sb.Append("| ").Append(a.DisplayName).Append(" | ").Append(channel)
                    .Append(" | ").Append(a.Messages)
                    .Append(" | ").Append(frt).Append(" | ").Append(a.AwaitingNow).AppendLine(" |");
            }
        }

        return sb.ToString();
    }

    internal static string FormatMinutes(double minutes)
    {
        if (minutes < 1)
        {
            return "<1 min";
        }

        if (minutes < 60)
        {
            return $"{Math.Round(minutes)} min";
        }

        var hours = minutes / 60.0;
        if (hours < 24)
        {
            return hours < 10 ? $"{hours.ToString("0.#", CultureInfo.InvariantCulture)} hr" : $"{Math.Round(hours)} hr";
        }

        return $"{Math.Round(hours / 24.0)} days";
    }
}
