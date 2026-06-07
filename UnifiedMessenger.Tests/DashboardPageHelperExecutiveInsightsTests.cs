using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardPageHelperExecutiveInsightsTests
{
    [Fact]
    public void BuildExecutiveInsights_FiltersByBranchInstance()
    {
        var triage = new MessageTriageService();
        triage.RestoreItems(
        [
            CreateInsightItem("f11", "Depilex F-11", "Sara", "Facial Saturday"),
            CreateInsightItem("d12", "Depilex D-12", "Noor", "Cancel booking")
        ]);

        var instances = new[]
        {
            new MessengerInstance { Id = "f11", DisplayName = "Depilex F-11", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "d12", DisplayName = "Depilex D-12", Category = WorkspaceCategory.Professional }
        };

        var insights = DashboardPageHelper.BuildExecutiveInsights(instances, "F-11", triage);

        Assert.Single(insights);
        Assert.Equal("Sara", insights[0].CustomerName);
        Assert.Equal("F-11", insights[0].BranchName);
        Assert.Contains(insights[0].Fields, field => field.Label == "Service" && field.Value == "Facial");
    }

    [Fact]
    public void BuildExecutiveInsights_OmitsEmptyEntityFields()
    {
        var triage = new MessageTriageService();
        triage.RestoreItems(
        [
            new MessageTriageItem
            {
                Id = "f11|1",
                InstanceId = "f11",
                InstanceDisplayName = "Depilex F-11",
                Platform = "googlebusiness",
                MessagePreview = "Need appointment",
                CustomerName = "Sara",
                UrgencyScore = 40,
                Sentiment = MessageSentiment.Neutral,
                InferenceSource = TriageInferenceSource.LocalAi,
                CoreSummary = "Booking inquiry",
                ExtractedEntities = new RichTriageExtractedEntities
                {
                    CustomerName = "Sara",
                    RequestedDate = "Friday"
                }
            }
        ]);

        var instances = new[]
        {
            new MessengerInstance { Id = "f11", DisplayName = "Depilex F-11", Category = WorkspaceCategory.Professional }
        };

        var card = Assert.Single(DashboardPageHelper.BuildExecutiveInsights(instances, triageService: triage));
        Assert.Equal(2, card.Fields.Count);
        Assert.DoesNotContain(card.Fields, field => field.Label == "Time");
        Assert.DoesNotContain(card.Fields, field => field.Label == "Contact");
    }

    [Fact]
    public void BuildExecutiveInsights_OmitsHeuristicWhenSettingDisabled()
    {
        var triage = new MessageTriageService();
        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "wa-1",
                Platform = "whatsapp",
                MessageText = "Need a haircut appointment tomorrow if possible please.",
                CustomerName = "Ali",
                ConversationHint = "Ali"
            },
            "WA");

        var cards = DashboardPageHelper.BuildExecutiveInsights(
            [new MessengerInstance { Id = "wa-1", DisplayName = "WA", Category = WorkspaceCategory.Professional }],
            triageService: triage,
            includeHeuristic: false);

        Assert.Empty(cards);
    }

    [Fact]
    public void HasExecutiveInsightContent_RequiresRichSignals()
    {
        var heuristicOnly = new MessageTriageItem
        {
            Id = "a|1",
            InstanceId = "a",
            InstanceDisplayName = "A",
            Platform = "metabusiness",
            MessagePreview = "Hello",
            UrgencyScore = 10,
            Sentiment = MessageSentiment.Neutral
        };

        Assert.False(DashboardPageHelper.HasExecutiveInsightContent(heuristicOnly));
        Assert.True(DashboardPageHelper.HasExecutiveInsightContent(CreateInsightItem("f11", "F-11", "Sara", "Book now")));
    }

    private static MessageTriageItem CreateInsightItem(
        string instanceId,
        string branchName,
        string customer,
        string summary) =>
        new()
        {
            Id = $"{instanceId}|{Guid.NewGuid():N}",
            InstanceId = instanceId,
            InstanceDisplayName = branchName,
            Platform = "googlebusiness",
            MessagePreview = summary,
            CustomerName = customer,
            UrgencyScore = 70,
            Sentiment = MessageSentiment.Neutral,
            InferenceSource = TriageInferenceSource.LocalAi,
            CoreSummary = summary,
            ExtractedEntities = new RichTriageExtractedEntities
            {
                CustomerName = customer,
                ServiceType = "Facial",
                ActionRequired = "Confirm slot"
            }
        };
}
