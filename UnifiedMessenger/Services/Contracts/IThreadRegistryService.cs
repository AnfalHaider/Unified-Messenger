using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IThreadRegistryService
{
    event EventHandler? Changed;

    IReadOnlyList<ThreadData> GetAllThreads();

    void RestoreThreads(IEnumerable<ThreadData> threads);

    void UpsertFromTriageItem(
        MessageTriageItem item,
        string? conversationKey,
        string? branchName,
        string? nextActionSummary = null,
        string? aiIntentCategory = null,
        string? clientSentiment = null,
        int? operationalUrgency = null,
        double? estimatedValue = null,
        bool? isRevenueLeakageRisk = null,
        bool? isSpamOrPromo = null,
        string? suggestedAction = null);

    void MarkThreadResolved(
        string instanceId,
        string? conversationKey,
        string? customerName,
        DateTimeOffset? resolvedAtUtc = null,
        string? platform = null);

    void SetThreadKanbanColumn(string threadId, UnifiedMessengerKanbanColumn targetColumn);

    void UpdateWhatsAppDeliveryStatus(
        string instanceId,
        string conversationKey,
        string status,
        DateTimeOffset? updatedAtUtc = null);

    void UpdateLastMessageKind(
        string instanceId,
        string conversationKey,
        InboundMessageKind messageKind,
        DateTimeOffset messageAtUtc);

    void RefreshOperationalFlags(bool raiseChanged = true);
}
