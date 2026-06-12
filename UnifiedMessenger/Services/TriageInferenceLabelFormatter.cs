using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class TriageInferenceLabelFormatter
{
    public static string Format(TriageInferenceSource source) =>
        source switch
        {
            TriageInferenceSource.Heuristic => "Heuristic",
            _ => source.ToString()
        };
}
