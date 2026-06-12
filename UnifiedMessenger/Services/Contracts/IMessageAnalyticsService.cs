using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IMessageAnalyticsService
{
    event EventHandler? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);

    Task ClearAllDataAsync(CancellationToken cancellationToken = default);

    void RecordMessageSent(
        string instanceId,
        string? chatHint = null,
        string? conversationKey = null,
        DateTimeOffset? sentAtUtc = null);

    void RecordMessageReceived(string instanceId, string? conversationKey = null, DateTimeOffset? receivedAtUtc = null);

    void NotifyDashboardRefresh();

    ProfessionalAnalyticsSnapshot CaptureProfessionalSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        NotificationHub notificationHub,
        string? selectedBranchKey = null);
}
