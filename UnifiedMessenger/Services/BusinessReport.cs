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
    int AwaitingNow);

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
    IReadOnlyList<AccountReportLine> Accounts);

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

        var insights = new List<BusinessInsight>();

        // ── Volume trend ────────────────────────────────────────────────────────────────────
        if (input.MessagesThisWeek > 0 || input.MessagesLastWeek > 0)
        {
            if (input.MessagesLastWeek == 0)
            {
                insights.Add(new BusinessInsight(InsightSeverity.Info,
                    "First week of tracked activity",
                    $"{input.MessagesThisWeek} customer messages this week — next week you'll get a week-over-week trend."));
            }
            else
            {
                var deltaPct = (int)Math.Round((input.MessagesThisWeek - input.MessagesLastWeek) * 100.0 / input.MessagesLastWeek);
                if (Math.Abs(deltaPct) >= 15)
                {
                    var dir = deltaPct > 0 ? "up" : "down";
                    insights.Add(new BusinessInsight(deltaPct > 0 ? InsightSeverity.Info : InsightSeverity.Warn,
                        $"Message volume is {dir} {Math.Abs(deltaPct)}% this week",
                        $"{input.MessagesThisWeek} messages vs {input.MessagesLastWeek} last week."
                        + (deltaPct > 0 ? " Make sure coverage keeps up." : " Quieter than usual.")));
                }
                else
                {
                    insights.Add(new BusinessInsight(InsightSeverity.Good,
                        "Volume steady",
                        $"{input.MessagesThisWeek} messages this week, about the same as last week."));
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
                    "Replies are slower this week",
                    $"Median first reply is {FormatMinutes(thisWk)} — up from {FormatMinutes(lastWk)} last week."));
            }
            else if (overSla)
            {
                insights.Add(new BusinessInsight(InsightSeverity.Warn,
                    "Reply speed is over your target",
                    $"Median first reply is {FormatMinutes(thisWk)} vs your {input.SlaThresholdMinutes}-minute target."));
            }
            else
            {
                insights.Add(new BusinessInsight(InsightSeverity.Good,
                    "Reply speed is healthy",
                    $"Median first reply is {FormatMinutes(thisWk)} across {input.FrtSamplesThisWeek} replies."));
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

        // ── Comparative call-outs across accounts ───────────────────────────────────────────
        var active = input.Accounts.Where(a => a.Messages > 0).ToList();
        if (active.Count > 1)
        {
            var top = active.OrderByDescending(a => a.Messages).First();
            var share = (int)Math.Round(top.Messages * 100.0 / Math.Max(1, active.Sum(a => a.Messages)));
            insights.Add(new BusinessInsight(InsightSeverity.Info,
                $"{top.DisplayName} is your busiest account",
                $"{top.Messages} messages this week — {share}% of all customer volume."));

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
                $"{a.AwaitingNow} waiting and no replies measured yet this week."));
        }

        // Warn first, then info, then good — most-actionable at the top.
        var ranked = insights
            .OrderBy(i => i.Severity switch { InsightSeverity.Warn => 0, InsightSeverity.Info => 1, _ => 2 })
            .ToList();

        return new BusinessReportResult(ranked, BuildSummary(input, ranked), BuildMarkdown(input, ranked));
    }

    private static string BuildSummary(ReportInputs input, IReadOnlyList<BusinessInsight> insights)
    {
        var warns = insights.Where(i => i.Severity == InsightSeverity.Warn).Take(2).ToList();
        if (warns.Count > 0)
        {
            return "Focus this week: " + string.Join("; ", warns.Select(w => w.Title.ToLowerInvariant())) + ".";
        }

        if (input.MessagesThisWeek == 0)
        {
            return "No customer activity recorded this week yet.";
        }

        return $"A solid week — {input.MessagesThisWeek} messages handled, "
            + (input.FrtSamplesThisWeek > 0 ? $"median reply {FormatMinutes(input.MedianFrtThisWeekMinutes)}, " : string.Empty)
            + $"{input.AwaitingNow} waiting now.";
    }

    private static string BuildMarkdown(ReportInputs input, IReadOnlyList<BusinessInsight> insights)
    {
        var sb = new StringBuilder();
        sb.Append("# Business report — ").AppendLine(input.PeriodLabel);
        sb.AppendLine();
        sb.AppendLine("## At a glance");
        sb.Append("- Customer messages this week: **").Append(input.MessagesThisWeek).Append("**");
        if (input.MessagesLastWeek > 0)
        {
            var d = input.MessagesThisWeek - input.MessagesLastWeek;
            sb.Append(" (").Append(d >= 0 ? "+" : "").Append(d).Append(" vs last week)");
        }

        sb.AppendLine();
        if (input.FrtSamplesThisWeek > 0)
        {
            sb.Append("- Median first reply: **").Append(FormatMinutes(input.MedianFrtThisWeekMinutes))
                .Append("** (").Append(input.FrtSamplesThisWeek).AppendLine(" replies measured)");
            sb.Append("- Replies within your ").Append(input.SlaThresholdMinutes).Append("-min target: **")
                .Append(input.SlaMetPercent).AppendLine("%**");
            sb.Append("- Replied to **").Append(input.AnsweredThisWeek).AppendLine("** waiting customers this week");
        }

        sb.Append("- Waiting on a reply right now: **").Append(input.AwaitingNow).AppendLine("**");
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
            sb.AppendLine("| Account | Messages | Median reply | Waiting now |");
            sb.AppendLine("|---|---:|---:|---:|");
            foreach (var a in active.OrderByDescending(a => a.Messages))
            {
                var frt = a.FrtSamples > 0 ? FormatMinutes(a.MedianFrtMinutes) : "—";
                sb.Append("| ").Append(a.DisplayName).Append(" | ").Append(a.Messages)
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
