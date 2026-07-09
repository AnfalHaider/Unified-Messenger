using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class AiSettingsSectionHelperTests
{
    [Fact]
    public void LoadCatalog_ReturnsEntriesFromJsonWhenPresent()
    {
        var catalog = AiSettingsSectionHelper.LoadCatalog(AppContext.BaseDirectory);

        Assert.NotEmpty(catalog);
        Assert.Contains(catalog, model => model.Id == "phi3:mini");
    }

    [Fact]
    public void DescribeOccAiChip_ReportsReadyWhenRunningAndEnabled()
    {
        var settings = new UnifiedMessenger.Models.AppSettings
        {
            EnableLocalAi = true
        };

        Assert.Equal("AI ready", AiSettingsSectionHelper.DescribeOccAiChip(
            settings,
            UnifiedMessenger.Services.Ai.OllamaConnectionState.Running));
    }

    [Fact]
    public void ShouldShowRuntimeDownloadButton_WhenRuntimeMissingAndNoSystemOllama()
    {
        Assert.True(AiSettingsSectionHelper.ShouldShowRuntimeDownloadButton(
            enableLocalAi: true,
            hasEmbeddedExecutable: false,
            systemOllamaHealthy: false));
        Assert.False(AiSettingsSectionHelper.ShouldShowRuntimeDownloadButton(
            enableLocalAi: true,
            hasEmbeddedExecutable: true,
            systemOllamaHealthy: false));
        Assert.False(AiSettingsSectionHelper.ShouldShowRuntimeDownloadButton(
            enableLocalAi: true,
            hasEmbeddedExecutable: false,
            systemOllamaHealthy: true));
    }

    [Fact]
    public void FormatPullProgress_IncludesPercentAndSpeed()
    {
        var progress = new OllamaPullProgress
        {
            Model = "phi3:mini",
            Status = "pulling",
            Completed = 50,
            Total = 100,
            IsComplete = false
        };

        var text = AiSettingsSectionHelper.FormatPullProgress(progress, 2 * 1024 * 1024);

        Assert.Contains("50%", text, StringComparison.Ordinal);
        Assert.Contains("MB/s", text, StringComparison.Ordinal);
    }
}
