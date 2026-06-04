using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

public sealed class MetaGoogleBackfillProvider : IBackfillSyncProvider
{
    public string PlatformId => "metabusiness";

    public bool CanBackfill(MessengerInstance instance) =>
        DashboardScrapeOrchestrator.IsDashboardScrapeCapable(instance);

    public async Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await DashboardScrapeOrchestrator.Instance
            .RefreshProfessionalInstancesAsync([context.Instance], cancellationToken)
            .ConfigureAwait(false);

        return new BackfillResult
        {
            TriageEnqueued = 0,
            AnalyticsInboundRecorded = 0,
            SlaCandidatesRecorded = 0
        };
    }
}
