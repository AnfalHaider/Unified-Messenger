using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests.Ollama;

public class OllamaApiModelTests
{
    [Fact]
    public void OllamaPullProgress_ComputesPercent()
    {
        var progress = new OllamaPullProgress
        {
            Model = "phi3:mini",
            Status = "downloading",
            Completed = 25,
            Total = 100,
            IsComplete = false
        };

        Assert.Equal(25, progress.PercentComplete, 1);
    }

    [Fact]
    public void TryDeserialize_ParsesGenerateChunk()
    {
        const string line = """{"response":"Hi","done":false}""";

        Assert.True(OllamaJson.TryDeserialize<OllamaGenerateChunk>(line.AsSpan(), out var chunk));
        Assert.Equal("Hi", chunk!.Response);
        Assert.False(chunk.Done);
    }
}
