using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardProfessionalDisplayTests
{
    [Fact]
    public void BuildProfessionalDisplay_ShowsInboundOnlyResponseRate_WhenNoReplyPairs()
    {
        var snapshot = new ProfessionalAnalyticsSnapshot
        {
            ReceivedCount = 5,
            ReplyPairCount = 0,
            HasMessageVolume = true,
            HasReplyMetrics = false
        };

        var display = DashboardPageHelper.BuildProfessionalDisplay(snapshot);

        Assert.Contains("Inbound: 5", display.ResponseRate, StringComparison.Ordinal);
        Assert.Contains("Replied: 0", display.ResponseRate, StringComparison.Ordinal);
        Assert.Equal("No replies logged yet", display.AverageReplyTime);
    }

    [Fact]
    public void BuildMetaResponseDisplay_ExposesPendingResponse_WhenInboundOnly()
    {
        var display = DashboardPageHelper.BuildMetaResponseDisplay(new MetaResponseEfficiencySnapshot
        {
            SampleCount = 0,
            LastInboundDisplay = "2m ago",
            ActiveUnreadCount = 3,
            EfficiencyRating = "Good",
            AverageResponseDisplay = "12 min"
        });

        Assert.True(display.InboundOnly);
        Assert.Equal("3 pending response", display.PendingResponseLabel);
    }

    [Fact]
    public void BuildExecutiveInsights_IncludesHeuristicCards_WhenEnabled()
    {
        AppSettingsService.Instance.Settings.ShowHeuristicExecutiveInsights = true;

        var triage = new MessageTriageService();
        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "wa-1",
                Platform = "whatsapp",
                MessageText = "Can I book a facial for next Friday afternoon?",
                CustomerName = "Noor",
                ConversationHint = "Noor"
            },
            "WA");

        var cards = DashboardPageHelper.BuildExecutiveInsights(
            [
                new MessengerInstance
                {
                    Id = "wa-1",
                    DisplayName = "WA",
                    Platform = "whatsapp",
                    Category = WorkspaceCategory.Professional
                }
            ],
            triageService: triage);

        Assert.NotEmpty(cards);
        Assert.Contains(cards, card => card.SourceLabel.Equals("Heuristic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DashboardScrapeStatusService_BuildsGoogleFooter()
    {
        DashboardScrapeStatusService.Instance.ClearForTests();
        DashboardScrapeStatusService.Instance.Record("g1", success: true, "dashboard-scrape", null);

        var footer = DashboardScrapeStatusService.Instance.BuildGoogleTrustScrapeFooter(["g1"]);

        Assert.Contains("Last dashboard scrape", footer, StringComparison.Ordinal);
        Assert.Contains("OK", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputeBarHeight_UsesMinimumForNonZeroValues()
    {
        var height = DashboardTriageHelper.ComputeBarHeight(1, maxValue: 100, maxHeight: 90);

        Assert.True(height >= DashboardTriageHelper.MinNonZeroBarHeight);
    }
}
