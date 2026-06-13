using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Tests.Ai;

public class HeuristicTriageProcessorTests
{
    [Fact]
    public void Process_BuildsHeuristicSummariesForBookingIntent()
    {
        var result = HeuristicTriageProcessor.Process(new MessageTriageRequest
        {
            InstanceId = "inst-1",
            InstanceDisplayName = "Branch",
            Platform = "whatsapp",
            MessageText = "Can I book an appointment for tomorrow afternoon?",
            CustomerName = "Sara"
        });

        Assert.NotNull(result);
        Assert.Equal(TriageInferenceSource.Heuristic, result!.Item.InferenceSource);
        Assert.Contains("Booking request", result.Item.CoreSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("booking", result.Item.NextActionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Reply", result.Item.SuggestedAction);
    }
}
