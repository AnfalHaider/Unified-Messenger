using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;
using UnifiedMessenger.Tests.Ai;

namespace UnifiedMessenger.Tests;

public class OccThreadCardPresenterTests
{
    [Fact]
    public void ResolveInferenceSource_UsesAnalyzingWhenQueueActive()
    {
        var settings = new AppSettings { EnableLocalAi = true };
        var registry = ThreadRegistryService.CreateForTests();
        var item = new MessageTriageItem
        {
            Id = "t1",
            InstanceId = "inst",
            InstanceDisplayName = "Shop",
            Platform = "whatsapp",
            MessagePreview = "Need help",
            ThreadId = "inst|chat",
            ConversationKey = "chat",
            UrgencyScore = 80
        };
        registry.UpsertFromTriageItem(item, "chat", "General");

        var client = new FakeAiInferenceClient { PingHandler = _ => true };
        var runtime = new OllamaRuntimeService(
            inferenceClient: client,
            settingsProvider: () => settings,
            downloadClient: new HttpClient(),
            runtimeInstallDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            embeddedExecutablePath: Path.Combine(Path.GetTempPath(), "missing", "ollama.exe"),
            modelsDirectory: Path.Combine(Path.GetTempPath(), "missing", "models"));
        runtime.EnsureRunningAsync().GetAwaiter().GetResult();

        var queue = new AiInferenceQueue(
            startBackgroundWorker: false,
            inferenceClient: client,
            runtimeService: runtime,
            settingsProvider: () => settings,
            threadRegistry: registry);

        var thread = registry.GetAllThreads()[0];
        queue.EnqueueIfEligible(item, allowLlmInference: true);

        Assert.Equal(
            TriageInferenceSource.Analyzing,
            OccThreadCardPresenter.ResolveInferenceSource(thread, queue));
    }
}
