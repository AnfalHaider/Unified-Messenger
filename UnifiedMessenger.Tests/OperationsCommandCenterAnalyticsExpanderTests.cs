using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OperationsCommandCenterAnalyticsExpanderTests
{
    [Fact]
    public void ShouldAutoExpand_ReturnsTrue_WhenImmediateActionPresent()
    {
        var status = new OperationsStatusSnapshot { ImmediateActionCount = 2 };
        var analytics = OperationsAnalyticsTrendSnapshot.Empty;

        Assert.True(DashboardCardEmptyStateHelper.ShouldAutoExpandAnalyticsTrends(status, analytics));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsTrue_WhenOpenThreadsHaveSlaBreaches()
    {
        var status = new OperationsStatusSnapshot
        {
            OpenThreadCount = 3,
            SlaBreachesNumeric = 1
        };
        var analytics = OperationsAnalyticsTrendSnapshot.Empty;

        Assert.True(DashboardCardEmptyStateHelper.ShouldAutoExpandAnalyticsTrends(status, analytics));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsTrue_WhenOperationalHighlightsPresent()
    {
        var status = OperationsStatusSnapshot.Empty;
        var analytics = new OperationsAnalyticsTrendSnapshot
        {
            Highlights =
            [
                new OperationalHighlightItem
                {
                    Title = "Peak hour",
                    Subtitle = "14:00",
                    InstanceDisplayName = "WhatsApp"
                }
            ]
        };

        Assert.True(DashboardCardEmptyStateHelper.ShouldAutoExpandAnalyticsTrends(status, analytics));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsTrue_WhenNegativeSentimentSpike()
    {
        var status = OperationsStatusSnapshot.Empty;
        var analytics = new OperationsAnalyticsTrendSnapshot
        {
            Triage = new MessageTriageDashboardSnapshot
            {
                PositiveCount = 1,
                NegativeCount = 2
            }
        };

        Assert.True(DashboardCardEmptyStateHelper.ShouldAutoExpandAnalyticsTrends(status, analytics));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsFalse_WhenNoActionableSignals()
    {
        Assert.False(DashboardCardEmptyStateHelper.ShouldAutoExpandAnalyticsTrends(
            OperationsStatusSnapshot.Empty,
            OperationsAnalyticsTrendSnapshot.Empty));
    }

    [Fact]
    public void ComputeAlertSignal_IncreasesWhenSlaAndHighlightsEscalate()
    {
        var baselineStatus = new OperationsStatusSnapshot { ImmediateActionCount = 1 };
        var escalatedStatus = new OperationsStatusSnapshot
        {
            ImmediateActionCount = 2,
            OpenThreadCount = 4,
            SlaBreachesNumeric = 2
        };

        var baselineAnalytics = new OperationsAnalyticsTrendSnapshot
        {
            Highlights =
            [
                new OperationalHighlightItem
                {
                    Title = "A",
                    Subtitle = "B",
                    InstanceDisplayName = "C"
                }
            ],
            Triage = new MessageTriageDashboardSnapshot { NegativeCount = 1 }
        };

        var escalatedAnalytics = new OperationsAnalyticsTrendSnapshot
        {
            Highlights =
            [
                new OperationalHighlightItem
                {
                    Title = "A",
                    Subtitle = "B",
                    InstanceDisplayName = "C"
                },
                new OperationalHighlightItem
                {
                    Title = "D",
                    Subtitle = "E",
                    InstanceDisplayName = "F"
                }
            ],
            Triage = new MessageTriageDashboardSnapshot { NegativeCount = 3 }
        };

        Assert.True(
            DashboardCardEmptyStateHelper.ComputeAnalyticsTrendsAlertSignal(escalatedStatus, escalatedAnalytics) >
            DashboardCardEmptyStateHelper.ComputeAnalyticsTrendsAlertSignal(baselineStatus, baselineAnalytics));
    }

    [Fact]
    public void ShouldExpandOccSection_RespectsDismissedSignalUntilEscalation()
    {
        Assert.False(DashboardCardEmptyStateHelper.ShouldExpandOccSection(alertSignal: 3, dismissedSignal: 3));
        Assert.False(DashboardCardEmptyStateHelper.ShouldExpandOccSection(alertSignal: 3, dismissedSignal: 5));
        Assert.True(DashboardCardEmptyStateHelper.ShouldExpandOccSection(alertSignal: 4, dismissedSignal: 3));
        Assert.False(DashboardCardEmptyStateHelper.ShouldExpandOccSection(alertSignal: 0, dismissedSignal: 0));
    }
}
