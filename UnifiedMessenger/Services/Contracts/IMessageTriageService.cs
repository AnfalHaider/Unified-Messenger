using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IMessageTriageService
{
    event EventHandler? Changed;

    void Enqueue(
        InboundMessageSelection selection,
        string? instanceDisplayName = null,
        string? branchKey = null,
        bool allowLlmInference = true,
        bool skipDedupeCheck = false);

    IReadOnlyList<MessageTriageItem> GetAllItems();

    MessageTriageDashboardSnapshot BuildSnapshot(IEnumerable<MessengerInstance> professionalInstances);

    void Shutdown();
}
