namespace UnifiedMessenger.Models.Ollama;

public sealed class OllamaRuntimeDownloadProgress
{
    public required string Phase { get; init; }

    public long Completed { get; init; }

    public long Total { get; init; }

    public bool IsComplete { get; init; }

    public string? Error { get; init; }

    public double PercentComplete =>
        Total > 0 ? Math.Clamp(Completed * 100.0 / Total, 0, 100) : 0;
}
