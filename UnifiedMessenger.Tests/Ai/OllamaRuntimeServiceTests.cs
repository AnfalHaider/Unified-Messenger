using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Tests.Ai;

public class OllamaRuntimeServiceTests
{
    [Fact]
    public void DefaultPaths_UseUnifiedMessengerOwnedDirectories()
    {
        Assert.EndsWith(Path.Combine("UnifiedMessenger", "ollama", "runtime"), OllamaOptions.RuntimeInstallDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("UnifiedMessenger", "ollama", "models"), OllamaOptions.ModelsDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.Combine(OllamaOptions.RuntimeInstallDirectory, "ollama.exe"), OllamaOptions.EmbeddedExecutablePath);
    }

    [Fact]
    public void HasEmbeddedExecutable_ReflectsInjectedPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "um-ollama-test", Guid.NewGuid().ToString("N"));
        var executablePath = Path.Combine(tempDir, "ollama.exe");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(executablePath, "stub");

        try
        {
            var service = new OllamaRuntimeService(
                inferenceClient: new FakeAiInferenceClient(),
                settingsProvider: () => new Models.AppSettings { EnableLocalAi = true },
                downloadClient: new HttpClient(),
                runtimeInstallDirectory: tempDir,
                embeddedExecutablePath: executablePath,
                modelsDirectory: Path.Combine(tempDir, "models"));

            Assert.True(service.HasEmbeddedExecutable);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureRunningAsync_WhenAiDisabled_ReturnsFalse()
    {
        var client = new FakeAiInferenceClient();
        var service = new OllamaRuntimeService(
            inferenceClient: client,
            settingsProvider: () => new Models.AppSettings { EnableLocalAi = false },
            downloadClient: new HttpClient(),
            runtimeInstallDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            embeddedExecutablePath: Path.Combine(Path.GetTempPath(), "missing", "ollama.exe"),
            modelsDirectory: Path.Combine(Path.GetTempPath(), "missing", "models"));

        var running = await service.EnsureRunningAsync();

        Assert.False(running);
        Assert.Equal(OllamaConnectionState.NotRunning, service.ConnectionState);
        Assert.Equal(0, client.PingCallCount);
    }

    [Fact]
    public async Task EnsureRunningAsync_WhenSystemOllamaHealthy_SkipsBootstrap()
    {
        var client = new FakeAiInferenceClient
        {
            PingHandler = _ => true
        };

        var service = new OllamaRuntimeService(
            inferenceClient: client,
            settingsProvider: () => new Models.AppSettings
            {
                EnableLocalAi = true,
                OllamaAutoBootstrap = true
            },
            downloadClient: new HttpClient(),
            runtimeInstallDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            embeddedExecutablePath: Path.Combine(Path.GetTempPath(), "missing", "ollama.exe"),
            modelsDirectory: Path.Combine(Path.GetTempPath(), "missing", "models"));

        var running = await service.EnsureRunningAsync();

        Assert.True(running);
        Assert.Equal(OllamaConnectionState.Running, service.ConnectionState);
        Assert.Equal(1, client.PingCallCount);
    }
}
