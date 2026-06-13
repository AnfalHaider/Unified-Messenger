using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardPageHelperInferenceTests
{
    [Fact]
    public void ResolveInsightSummary_UsesPreviewInsteadOfAwaitingAiCopy()
    {
        var item = new MessageTriageItem
        {
            Id = "a",
            InstanceId = "inst",
            InstanceDisplayName = "Shop",
            Platform = "whatsapp",
            MessagePreview = "Can I book for tomorrow?",
            AiIntentCategory = UnifiedMessengerIntentCategory.Inquiry
        };

        var card = DashboardPageHelper.BuildHeuristicInsightCard(item);

        Assert.Equal("Can I book for tomorrow?", card.CoreSummary);
        Assert.DoesNotContain("Awaiting AI", card.CoreSummary);
    }

    [Fact]
    public void BuildExecutiveInsightCard_DefaultSourceLabelIsHeuristic()
    {
        var item = new MessageTriageItem
        {
            Id = "a",
            InstanceId = "inst",
            InstanceDisplayName = "Shop",
            Platform = "whatsapp",
            MessagePreview = "Hello",
            NextActionSummary = "Reply with availability"
        };

        var card = DashboardPageHelper.BuildExecutiveInsightCard(item, "Heuristic");

        Assert.Equal("Heuristic", card.SourceLabel);
    }
}
