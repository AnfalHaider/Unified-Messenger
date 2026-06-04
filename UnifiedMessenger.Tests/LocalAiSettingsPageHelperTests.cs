using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class LocalAiSettingsPageHelperTests
{
    [Fact]
    public void LoadCatalog_ReturnsEntriesFromJsonWhenPresent()
    {
        var baseDirectory = Path.Combine(AppContext.BaseDirectory);
        var catalog = LocalAiSettingsPageHelper.LoadCatalog(baseDirectory);

        Assert.NotEmpty(catalog);
        Assert.Contains(catalog, model => model.Id == "phi3:mini");
    }

    [Fact]
    public void IsModelInstalled_MatchesNamePrefix()
    {
        var installed = new[] { "phi3:mini", "llama3.2:latest" };

        Assert.True(LocalAiSettingsPageHelper.IsModelInstalled("phi3:mini", installed));
        Assert.True(LocalAiSettingsPageHelper.IsModelInstalled("llama3.2", installed));
        Assert.False(LocalAiSettingsPageHelper.IsModelInstalled("mistral", installed));
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

        var text = LocalAiSettingsPageHelper.FormatPullProgress(progress, 2 * 1024 * 1024);

        Assert.Contains("50%", text, StringComparison.Ordinal);
        Assert.Contains("MB/s", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TryComputeBytesPerSecond_ReturnsNullForTinyDelta()
    {
        var now = DateTimeOffset.UtcNow;
        var rate = LocalAiSettingsPageHelper.TryComputeBytesPerSecond(0, 100, now, now);

        Assert.Null(rate);
    }

    [Fact]
    public void TryComputeBytesPerSecond_ComputesPositiveRate()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddSeconds(2);
        var rate = LocalAiSettingsPageHelper.TryComputeBytesPerSecond(0, 2048, start, end);

        Assert.NotNull(rate);
        Assert.True(rate > 0);
    }
}
