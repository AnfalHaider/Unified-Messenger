using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Locks the deterministic insight/anomaly logic behind the weekly business report: response-time
/// degradation, SLA banding, backlog, and comparative account call-outs.
/// </summary>
public class BusinessReportTests
{
    private static ReportInputs Base(params AccountReportLine[] accounts) => new(
        PeriodLabel: "This week",
        MessagesThisWeek: 100,
        MessagesLastWeek: 100,
        MedianFrtThisWeekMinutes: 8,
        FrtSamplesThisWeek: 20,
        MedianFrtLastWeekMinutes: 8,
        FrtSamplesLastWeek: 20,
        SlaMetPercent: 95,
        SlaThresholdMinutes: 15,
        AnsweredThisWeek: 18,
        AwaitingNow: 0,
        BusiestDay: "Monday",
        BusiestHour: "7 PM",
        Accounts: accounts);

    [Fact]
    public void Build_ResponseTimeDegradation_RaisesWarnFirst()
    {
        var input = Base() with { MedianFrtThisWeekMinutes = 30, MedianFrtLastWeekMinutes = 9 };

        var report = BusinessReport.Build(input);

        Assert.Contains(report.Insights, i => i.Severity == InsightSeverity.Warn && i.Title.Contains("slower", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(InsightSeverity.Warn, report.Insights[0].Severity); // warnings ranked first
        Assert.Contains("focus this week", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_SmallFrtIncrease_DoesNotCryWolf()
    {
        // 1 min → 3 min is a 3x jump but below the floor, so it should NOT flag as degraded.
        var input = Base() with { MedianFrtThisWeekMinutes = 3, MedianFrtLastWeekMinutes = 1 };

        var report = BusinessReport.Build(input);

        Assert.DoesNotContain(report.Insights, i => i.Title.Contains("slower", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Insights, i => i.Severity == InsightSeverity.Good && i.Title.Contains("healthy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_LowSla_IsWarned()
    {
        var report = BusinessReport.Build(Base() with { SlaMetPercent = 40 });

        Assert.Contains(report.Insights, i => i.Severity == InsightSeverity.Warn && i.Title.Contains("40%"));
    }

    [Fact]
    public void Build_VolumeSpike_Flagged()
    {
        var report = BusinessReport.Build(Base() with { MessagesThisWeek = 200, MessagesLastWeek = 100 });

        Assert.Contains(report.Insights, i => i.Title.Contains("up 100%", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_BusiestAndSlowestAccounts_CalledOut()
    {
        var report = BusinessReport.Build(Base(
            new AccountReportLine("Sales", 80, 5, 10, 0),
            new AccountReportLine("Support", 20, 40, 8, 2)));

        Assert.Contains(report.Insights, i => i.Title.Contains("Sales") && i.Title.Contains("busiest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Insights, i => i.Title.Contains("Support") && i.Title.Contains("slowest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_NeglectedAccount_Flagged()
    {
        // Backlog but no measured replies → neglected.
        var report = BusinessReport.Build(Base(new AccountReportLine("Support", 0, 0, 0, 5)));

        Assert.Contains(report.Insights, i => i.Title.Contains("neglected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_Markdown_ContainsSectionsAndAccountTable()
    {
        var report = BusinessReport.Build(Base(new AccountReportLine("Sales", 80, 5, 10, 1)));

        Assert.Contains("# Business report", report.Markdown);
        Assert.Contains("## At a glance", report.Markdown);
        Assert.Contains("## By account", report.Markdown);
        Assert.Contains("| Sales |", report.Markdown);
    }

    [Fact]
    public void Build_HealthyWeek_SummaryIsPositive()
    {
        var report = BusinessReport.Build(Base());

        Assert.DoesNotContain(report.Insights, i => i.Severity == InsightSeverity.Warn);
        Assert.Contains("solid week", report.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
