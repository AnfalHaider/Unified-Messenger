using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-METRICS-07 — the weekly report's share percentages must not round into a self-contradicting sentence.
///
/// Two share figures in the report are bounded 0–100 and were rounded with plain <c>Math.Round</c>:
/// the busiest account's share of volume, and the returning-customer rate. Rounding either up to 100
/// produces copy that contradicts itself in the same sentence — "100% of all customer volume" when a
/// second account is demonstrably active, and "100% … had contacted you before; 3 reached out for the
/// first time."
///
/// Note the volume DELTA (<c>MessagesThisWeek</c> vs <c>MessagesLastWeek</c>) is deliberately excluded:
/// that is a change percentage, legitimately unbounded and free to exceed 100.
/// </summary>
public class BusinessReportSharePercentTests
{
    private static ReportInputs Inputs(
        IReadOnlyList<AccountReportLine> accounts,
        int newCustomers = 0,
        int returningCustomers = 0,
        bool hasHistory = false) =>
        new(
            PeriodLabel: "1–7 Aug",
            MessagesThisWeek: accounts.Sum(a => a.Messages),
            MessagesLastWeek: accounts.Sum(a => a.Messages),
            MedianFrtThisWeekMinutes: 5,
            FrtSamplesThisWeek: 10,
            MedianFrtLastWeekMinutes: 5,
            FrtSamplesLastWeek: 10,
            SlaMetPercent: 95,
            SlaThresholdMinutes: 15,
            AnsweredThisWeek: 10,
            AwaitingNow: 0,
            BusiestDay: "Tue",
            BusiestHour: "2 PM",
            Accounts: accounts,
            NewCustomersThisWeek: newCustomers,
            ReturningCustomersThisWeek: returningCustomers,
            HasCustomerHistory: hasHistory);

    private static AccountReportLine Account(string name, int messages) =>
        new(name, messages, MedianFrtMinutes: 5, FrtSamples: 5, AwaitingNow: 0);

    private static string AllText(BusinessReportResult report) =>
        string.Join("\n", report.Insights.Select(i => $"{i.Title} {i.Detail}"));

    [Fact]
    public void TheBusiestAccountsShareIsNotRoundedUpToOneHundredWhileAnotherAccountIsActive()
    {
        // 996 of 1000 is 99.6% -> rounds to 100, in a call-out that only fires when 2+ accounts are active.
        var report = BusinessReport.Build(Inputs([Account("Main", 996), Account("Branch", 4)]));

        var text = AllText(report);

        Assert.Contains("busiest account", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100% of all customer volume", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASoleDominantAccountStillReadsSensibly()
    {
        // Guard against over-correcting: a genuinely lopsided split should still report a high share.
        var report = BusinessReport.Build(Inputs([Account("Main", 900), Account("Branch", 100)]));

        Assert.Contains("90% of all customer volume", AllText(report), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReturnRateIsNotRoundedUpToOneHundredWhileNewCustomersExist()
    {
        // "100% … had contacted you before; 3 reached out for the first time." contradicts itself.
        var report = BusinessReport.Build(Inputs(
            [Account("Main", 500)],
            newCustomers: 3,
            returningCustomers: 997,
            hasHistory: true));

        var text = AllText(report);

        Assert.Contains("reached out for the first time", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100% of the", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReturnRateIsNotRoundedDownToZeroWhileReturningCustomersExist()
    {
        var report = BusinessReport.Build(Inputs(
            [Account("Main", 500)],
            newCustomers: 997,
            returningCustomers: 3,
            hasHistory: true));

        Assert.DoesNotContain("0% of the", AllText(report), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnAllReturningWeekStillReportsOneHundredPercent()
    {
        // The honest 100% must survive.
        var report = BusinessReport.Build(Inputs(
            [Account("Main", 500)],
            newCustomers: 0,
            returningCustomers: 40,
            hasHistory: true));

        Assert.Contains("100% of the 40 customers", AllText(report), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheVolumeDeltaIsStillAllowedToExceedOneHundredPercent()
    {
        // A change percentage is not a share — tripling volume is legitimately "up 200%".
        var inputs = Inputs([Account("Main", 300)]) with { MessagesThisWeek = 300, MessagesLastWeek = 100 };

        Assert.Contains("up 200%", AllText(BusinessReport.Build(inputs)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSummaryPreservesAccountNameCasing()
    {
        // The summary is the report's headline sentence. Lower-casing insight titles mangled the owner's
        // own branch names — "Focus this week: depilex dha-2 whatsapp may be neglected".
        var neglected = new AccountReportLine(
            "Depilex DHA-2 WhatsApp", Messages: 40, MedianFrtMinutes: 5, FrtSamples: 0, AwaitingNow: 6);

        var report = BusinessReport.Build(Inputs([neglected, Account("Other", 10)]) with { AwaitingNow = 6 });

        Assert.Contains("Depilex DHA-2 WhatsApp", report.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("depilex dha-2 whatsapp", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void AFirstPeriodWithNoPriorVolumeDoesNotDivideByZero()
    {
        var inputs = Inputs([Account("Main", 50)]) with { MessagesThisWeek = 50, MessagesLastWeek = 0 };

        var report = BusinessReport.Build(inputs);

        Assert.Contains("First week of tracked activity", AllText(report), StringComparison.OrdinalIgnoreCase);
    }
}
