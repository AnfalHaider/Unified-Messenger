namespace UnifiedMessenger.Models.Ollama;

public sealed class OllamaPullProgress
{
    public required string Model { get; init; }

    public required string Status { get; init; }

    public long Completed { get; init; }

    public long Total { get; init; }

    public bool IsComplete { get; init; }

    public string? Error { get; init; }

    public double PercentComplete =>
        Total > 0 ? Math.Clamp(Completed * 100.0 / Total, 0, 100) : 0;

    public static OllamaPullProgress FromStatus(string model, string status) =>
        new()
        {
            Model = model,
            Status = status,
            Completed = 0,
            Total = 0,
            IsComplete = status.Contains("success", StringComparison.OrdinalIgnoreCase)
        };
}
