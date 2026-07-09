using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

public interface IBackfillSyncProvider
{
    string PlatformId { get; }

    bool CanBackfill(MessengerInstance instance);

    Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken);
}
