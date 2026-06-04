namespace UnifiedMessenger.Models.Ollama;

public enum OllamaConnectionState
{
    Unknown = 0,
    NotRunning,
    Starting,
    Running,
    Error
}
