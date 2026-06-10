using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Forces professional platform scrapers to publish fresh telemetry into the dashboard pipeline.
/// </summary>
public sealed class DashboardScrapeOrchestrator
{
    private static readonly Lazy<DashboardScrapeOrchestrator> LazyInstance =
        new(() => new DashboardScrapeOrchestrator());

    public static DashboardScrapeOrchestrator Instance => LazyInstance.Value;

    public async Task RefreshProfessionalInstancesAsync(
        IEnumerable<MessengerInstance> instances,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instances);

        foreach (var instance in instances.Where(IsDashboardScrapeCapable))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WebViewScriptGateway.Instance
                .ExecutePreparedScriptAsync(instance.Id, BuildForceScrapeScript(instance), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal static string BuildForceScrapeScript(MessengerInstance instance)
    {
        var instanceId = JsonSerializer.Serialize(instance.Id);
        var platform = JsonSerializer.Serialize(PlatformDefinition.NormalizePlatformId(instance.Platform));

        return "(function () {" +
               "try {" +
               "if (typeof window.__umForceDashboardScrape === 'function') { window.__umForceDashboardScrape(); return; }" +
               "if (typeof window.__unifiedMessengerPublishBadge === 'function') { window.__unifiedMessengerPublishBadge(); }" +
               "} catch (e) {" +
               "if (typeof window.__umPublishDashboardScrapeStatus === 'function') {" +
               $"window.__umPublishDashboardScrapeStatus({instanceId},{platform},false,'force-scrape',String(e&&e.message?e.message:e));" +
               "}" +
               "}" +
               "})();";
    }

    public static bool IsDashboardScrapeCapable(MessengerInstance instance) =>
        PlatformModules.PlatformModuleRegistry.Instance.IsEnabled(instance.Platform) &&
        (instance.Platform.Equals("metabusiness", StringComparison.OrdinalIgnoreCase) ||
         instance.Platform.Equals("googlebusiness", StringComparison.OrdinalIgnoreCase));
}
