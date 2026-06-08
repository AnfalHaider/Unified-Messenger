using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OperationsCommandCenterPlatformIntelligenceTests
{
    [Fact]
    public void ShouldAutoExpand_ReturnsTrue_WhenGoogleHasUnrepliedReviews()
    {
        var platform = new OperationsPlatformIntelligenceSnapshot
        {
            HasGoogleInstances = true,
            CustomerTrust = new CustomerTrustSnapshot
            {
                TotalUnrepliedReviews = 2
            }
        };

        Assert.True(DashboardCardEmptyStateHelper.ShouldAutoExpandPlatformIntelligence(platform));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsTrue_WhenGoogleHasPendingReviewAlerts()
    {
        var platform = new OperationsPlatformIntelligenceSnapshot
        {
            HasGoogleInstances = true,
            CustomerTrust = new CustomerTrustSnapshot
            {
                PendingReviews =
                [
                    new GoogleReviewAlert
                    {
                        Id = "g1:rev-1",
                        InstanceId = "g1",
                        InstanceDisplayName = "Google",
                        ReviewId = "rev-1",
                        ReviewerName = "Alex",
                        Snippet = "Great service",
                        LocationLabel = "Branch",
                        Rating = 5,
                        DetectedAt = DateTimeOffset.UtcNow
                    }
                ]
            }
        };

        Assert.True(DashboardCardEmptyStateHelper.ShouldAutoExpandPlatformIntelligence(platform));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsTrue_WhenMetaHasPendingInbound()
    {
        var platform = new OperationsPlatformIntelligenceSnapshot
        {
            HasMetaInstances = true,
            MetaResponse = new MetaResponseEfficiencySnapshot
            {
                ActiveUnreadCount = 3,
                LastInboundDisplay = "2m ago"
            }
        };

        Assert.True(DashboardCardEmptyStateHelper.ShouldAutoExpandPlatformIntelligence(platform));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsFalse_WhenNoPlatformInstances()
    {
        Assert.False(DashboardCardEmptyStateHelper.ShouldAutoExpandPlatformIntelligence(
            OperationsPlatformIntelligenceSnapshot.Empty));
    }

    [Fact]
    public void ShouldAutoExpand_ReturnsFalse_WhenConnectedAwaitingScrapeOnly()
    {
        var platform = new OperationsPlatformIntelligenceSnapshot
        {
            HasMetaInstances = true,
            MetaResponse = new MetaResponseEfficiencySnapshot()
        };

        Assert.False(DashboardCardEmptyStateHelper.ShouldAutoExpandPlatformIntelligence(platform));
    }

    [Fact]
    public void ComputeAlertSignal_IncreasesWhenNewGoogleAndMetaAlertsArrive()
    {
        var baseline = new OperationsPlatformIntelligenceSnapshot
        {
            HasGoogleInstances = true,
            HasMetaInstances = true,
            CustomerTrust = new CustomerTrustSnapshot { TotalUnrepliedReviews = 1 },
            MetaResponse = new MetaResponseEfficiencySnapshot { ActiveUnreadCount = 1 }
        };

        var escalated = new OperationsPlatformIntelligenceSnapshot
        {
            HasGoogleInstances = true,
            HasMetaInstances = true,
            CustomerTrust = new CustomerTrustSnapshot { TotalUnrepliedReviews = 3 },
            MetaResponse = new MetaResponseEfficiencySnapshot
            {
                ActiveUnreadCount = 2,
                LastInboundDisplay = "1m ago"
            }
        };

        Assert.True(
            DashboardCardEmptyStateHelper.ComputePlatformIntelligenceAlertSignal(escalated) >
            DashboardCardEmptyStateHelper.ComputePlatformIntelligenceAlertSignal(baseline));
    }

    [Fact]
    public void ShouldExpandOccSection_RequiresSignalAboveDismissedValue()
    {
        Assert.True(DashboardCardEmptyStateHelper.ShouldExpandOccSection(alertSignal: 2, dismissedSignal: 1));
        Assert.False(DashboardCardEmptyStateHelper.ShouldExpandOccSection(alertSignal: 2, dismissedSignal: 2));
    }
}
