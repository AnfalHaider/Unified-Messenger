using UnifiedMessenger.Models.Ai;

namespace UnifiedMessenger.Services.Ai;

public interface IAiInferenceClient
{
    Task<bool> TryPingAsync(CancellationToken cancellationToken = default);

    Task<AiInferenceResult?> GenerateStructuredAsync(
        string transcript,
        string modelName,
        CancellationToken cancellationToken = default);
}
