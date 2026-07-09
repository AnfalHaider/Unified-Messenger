using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class TriageInferenceLabelFormatterTests
{
    [Theory]
    [InlineData(TriageInferenceSource.Heuristic, "Rules")]
    [InlineData(TriageInferenceSource.Ollama, "AI")]
    [InlineData(TriageInferenceSource.Analyzing, "Analyzing…")]
    public void Format_MapsInferenceSources(TriageInferenceSource source, string expected) =>
        Assert.Equal(expected, TriageInferenceLabelFormatter.Format(source));

    [Fact]
    public void IsActiveJob_OnlyAnalyzingIsActive() =>
        Assert.True(TriageInferenceLabelFormatter.IsActiveJob(TriageInferenceSource.Analyzing));
}
