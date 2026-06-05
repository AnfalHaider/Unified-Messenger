using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

/// <summary>
/// Ensures background triage LLM jobs yield while interactive copilot streaming is active.
/// </summary>
public static class TriageInferencePriorityBroker
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    public static async Task WaitForBackgroundSlotAsync(CancellationToken cancellationToken = default)
    {
        while (OllamaInferenceCoordinator.Instance.IsInteractiveActive)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public static bool ShouldDeferBackgroundInference =>
        OllamaInferenceCoordinator.Instance.IsInteractiveActive;
}
