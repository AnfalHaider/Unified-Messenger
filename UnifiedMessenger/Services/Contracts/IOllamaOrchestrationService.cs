namespace UnifiedMessenger.Services.Ollama;

public interface IOllamaOrchestrationService : IDisposable
{
    void WarmupInBackground();
}
