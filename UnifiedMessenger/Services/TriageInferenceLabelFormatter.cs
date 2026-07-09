using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class TriageInferenceLabelFormatter
{
    public static string Format(TriageInferenceSource source) =>
        source switch
        {
            TriageInferenceSource.Heuristic => "Rules",
            TriageInferenceSource.Ollama => "AI",
            TriageInferenceSource.Analyzing => "Analyzing…",
            _ => "Heuristic"
        };

    public static bool IsActiveJob(TriageInferenceSource source) =>
        source == TriageInferenceSource.Analyzing;
}
