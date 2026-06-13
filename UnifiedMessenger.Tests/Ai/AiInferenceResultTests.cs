using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ai;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Tests.Ai;

public sealed class FakeAiInferenceClient : IAiInferenceClient
{
    public Func<string, string, CancellationToken, AiInferenceResult?>? GenerateHandler { get; set; }

    public Func<CancellationToken, bool>? PingHandler { get; set; }

    public int GenerateCallCount { get; private set; }

    public int PingCallCount { get; private set; }

    public Task<bool> TryPingAsync(CancellationToken cancellationToken = default)
    {
        PingCallCount++;
        return Task.FromResult(PingHandler?.Invoke(cancellationToken) ?? true);
    }

    public Task<AiInferenceResult?> GenerateStructuredAsync(
        string transcript,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        GenerateCallCount++;
        return Task.FromResult(GenerateHandler?.Invoke(transcript, modelName, cancellationToken));
    }
}

public class AiInferenceResultTests
{
    [Fact]
    public void TryParse_ValidJson_ReturnsNormalizedResult()
    {
        const string json = """
            {
              "intent": "Booking",
              "next_action": "Confirm appointment details",
              "suggested_action": "Reply"
            }
            """;

        var result = AiInferenceResult.TryParse(json);

        Assert.NotNull(result);
        Assert.Equal(UnifiedMessengerIntentCategory.Booking, result.Intent);
        Assert.Equal("Confirm appointment details", result.NextAction);
        Assert.Equal("Reply", result.SuggestedAction);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsNull()
    {
        Assert.Null(AiInferenceResult.TryParse("{not-json"));
    }

    [Fact]
    public async Task FakeClient_GenerateStructuredAsync_UsesHandler()
    {
        var client = new FakeAiInferenceClient
        {
            GenerateHandler = (_, _, _) => new AiInferenceResult
            {
                Intent = UnifiedMessengerIntentCategory.Lead,
                NextAction = "Follow up on lead",
                SuggestedAction = "Follow_up"
            }
        };

        var result = await client.GenerateStructuredAsync("Customer: Ali", "phi3:mini");

        Assert.NotNull(result);
        Assert.Equal(1, client.GenerateCallCount);
        Assert.Equal("Follow up on lead", result!.NextAction);
    }
}
