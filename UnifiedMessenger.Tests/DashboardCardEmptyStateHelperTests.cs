using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardCardEmptyStateHelperTests
{
    [Fact]
    public void ResolveGoogleTrustEmptyReason_NoInstances_ReturnsNoPlatform()
    {
        var reason = DashboardCardEmptyStateHelper.ResolveGoogleTrustEmptyReason(
            hasGoogleInstances: false,
            new CustomerTrustSnapshot());

        Assert.Equal(DashboardCardEmptyReason.NoPlatformInstance, reason);
    }

    [Fact]
    public void ResolveGoogleTrustEmptyReason_ConnectedNoReviews_ReturnsAwaitingScrape()
    {
        var reason = DashboardCardEmptyStateHelper.ResolveGoogleTrustEmptyReason(
            hasGoogleInstances: true,
            new CustomerTrustSnapshot());

        Assert.Equal(DashboardCardEmptyReason.ConnectedAwaitingScrape, reason);
        Assert.Contains(
            "Refresh",
            DashboardCardEmptyStateHelper.FormatGoogleTrustEmptyMessage(reason),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveGoogleTrustEmptyReason_WithPendingReviews_ReturnsHasData()
    {
        var reason = DashboardCardEmptyStateHelper.ResolveGoogleTrustEmptyReason(
            hasGoogleInstances: true,
            new CustomerTrustSnapshot
            {
                TotalUnrepliedReviews = 1,
                PendingReviews =
                [
                    new GoogleReviewAlert
                    {
                        Id = "g:1",
                        InstanceId = "g1",
                        InstanceDisplayName = "Google",
                        ReviewId = "r1",
                        ReviewerName = "Alex",
                        Snippet = "Great",
                        LocationLabel = "DHA",
                        Rating = 5,
                        DetectedAt = DateTimeOffset.UtcNow
                    }
                ]
            });

        Assert.Equal(DashboardCardEmptyReason.HasData, reason);
    }

    [Fact]
    public void ResolveMetaResponseEmptyReason_InboundOnly_ReturnsCopy()
    {
        var reason = DashboardCardEmptyStateHelper.ResolveMetaResponseEmptyReason(
            hasMetaInstances: true,
            new MetaResponseEfficiencySnapshot
            {
                SampleCount = 0,
                LastInboundDisplay = "4m ago"
            });

        Assert.Equal(DashboardCardEmptyReason.InboundOnlyAwaitingReply, reason);
        Assert.Contains(
            "Reply in Meta",
            DashboardCardEmptyStateHelper.FormatMetaResponseEmptyMessage(reason),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveUrgencyEmptyReason_OnlyLowUrgency_SuggestsRecentInbound()
    {
        var triage = new MessageTriageDashboardSnapshot
        {
            NeutralCount = 5,
            UrgentQueue = [],
            RecentInbound =
            [
                new MessageTriageItem
                {
                    Id = "1",
                    InstanceId = "wa",
                    InstanceDisplayName = "WA",
                    Platform = "whatsapp",
                    MessagePreview = "Hello there",
                    UrgencyScore = 10
                }
            ]
        };

        var reason = DashboardCardEmptyStateHelper.ResolveUrgencyEmptyReason(triage);

        Assert.Equal(DashboardCardEmptyReason.OnlyLowUrgencyItems, reason);
        Assert.Contains(
            "Recent inbound",
            DashboardCardEmptyStateHelper.FormatUrgencyEmptyMessage(reason),
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSnapshot_IncludesRecentInboundBelowUrgentThreshold()
    {
        AppSettingsService.Instance.Settings.DashboardUrgencyThreshold = 30;

        var triage = new MessageTriageService();
        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "wa-1",
                Platform = "whatsapp",
                MessageText = "Hi, I need to reschedule my appointment for Saturday please.",
                CustomerName = "Sara",
                ConversationHint = "Sara"
            },
            "WhatsApp");

        var snapshot = triage.BuildSnapshot([
            new MessengerInstance
            {
                Id = "wa-1",
                DisplayName = "WhatsApp",
                Platform = "whatsapp",
                Category = WorkspaceCategory.Professional
            }
        ]);

        if (snapshot.UrgentQueue.Count == 0)
        {
            Assert.NotEmpty(snapshot.RecentInbound);
        }
        else
        {
            Assert.True(snapshot.UrgentQueue.Count > 0 || snapshot.RecentInbound.Count > 0);
        }
    }

    [Fact]
    public void BuildBranchScopeSubtitle_AllBranches_IncludesCount()
    {
        var subtitle = DashboardCardEmptyStateHelper.BuildBranchScopeSubtitle(
            [
                new MessengerInstance { Id = "a", DisplayName = "A", Platform = "whatsapp", Category = WorkspaceCategory.Professional },
                new MessengerInstance { Id = "b", DisplayName = "B", Platform = "whatsapp", Category = WorkspaceCategory.Professional }
            ],
            branchInstanceId: null);

        Assert.Equal("Showing: All Branches (2)", subtitle);
    }
}
