using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests;

[CollectionDefinition(UnifiedMessengerSerialCollection.Name)]
public sealed class UnifiedMessengerSerialCollection : ICollectionFixture<UnifiedMessengerSerialGate>
{
    public const string Name = "UnifiedMessengerSerial";
}

public sealed class UnifiedMessengerSerialGate
{
    public UnifiedMessengerSerialGate()
    {
        ThreadRegistryService.Instance.RestoreThreads([]);
        MessageTriageService.Instance.ResetForTests([]);
        OllamaInferenceCoordinator.Instance.SetActivityForTests(OllamaInferenceActivity.Idle);
    }
}
