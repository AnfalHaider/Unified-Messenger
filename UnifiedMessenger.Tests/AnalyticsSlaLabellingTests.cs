using UnifiedMessenger.Pages;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Stops the Analytics page from putting two different "SLA met" numbers on one screen again.
/// </summary>
/// <remarks>
/// The KPI card's "SLA Met" is measured from first-response-time samples. The donut beside it is
/// apportioned by each entity's <c>OnTimePercent</c>, which on the live chat-snapshot path is the
/// caught-up proxy (unread cleared), over a denominator that also includes threads we cannot time at all.
/// Both were labelled "SLA met". They can disagree by any amount, and the owner had no way to tell which
/// was which.
/// </remarks>
public class AnalyticsSlaLabellingTests
{
    [Fact]
    public void DonutSlices_AreNotLabelledAsSla()
    {
        var slices = AnalyticsPage.BuildSlaSlices(new SlaBreakdown(Met: 6, Missed: 3, NoSla: 1));

        Assert.All(slices, s =>
            Assert.False(
                s.Label.Contains("SLA", StringComparison.OrdinalIgnoreCase),
                $"Slice '{s.Label}' claims to be an SLA outcome; it is apportioned by caught-up %."));
    }

    [Fact]
    public void DonutSlices_SayWhatTheyActuallyMeasure()
    {
        var slices = AnalyticsPage.BuildSlaSlices(new SlaBreakdown(Met: 6, Missed: 3, NoSla: 1));

        Assert.Equal(["Caught up", "Behind", "Not measured"], slices.Select(s => s.Label).ToArray());
    }

    [Fact]
    public void CaughtUpPercent_NeverPrintsOneHundredWhileAThreadIsBehind()
    {
        // 996 of 1000 — the case plain Math.Round turned into "100%" beside a visible Behind slice.
        var percent = AnalyticsPage.CaughtUpPercent(new SlaBreakdown(Met: 996, Missed: 4, NoSla: 0));

        Assert.Equal(99, percent);
    }

    [Fact]
    public void CaughtUpPercent_ReservesOneHundredForNothingOutstanding() =>
        Assert.Equal(100, AnalyticsPage.CaughtUpPercent(new SlaBreakdown(Met: 12, Missed: 0, NoSla: 0)));

    [Fact]
    public void CaughtUpPercent_IsZeroWhenThereIsNothingToMeasure() =>
        Assert.Equal(0, AnalyticsPage.CaughtUpPercent(new SlaBreakdown(Met: 0, Missed: 0, NoSla: 0)));

    [Fact]
    public void CaughtUpPercent_CountsUntimeableThreadsInTheDenominator()
    {
        // The donut's whole point: threads we cannot time are neither caught up nor behind, but they are
        // still part of the business, so they must dilute the share rather than vanish from it.
        var percent = AnalyticsPage.CaughtUpPercent(new SlaBreakdown(Met: 5, Missed: 0, NoSla: 5));

        Assert.Equal(50, percent);
    }
}
