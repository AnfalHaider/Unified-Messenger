using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Services;

public sealed class DashboardInstanceHealthChip
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string Platform { get; init; }

    public required string BackfillState { get; init; }

    public required string AdapterHealth { get; init; }

    public int TriageItemCount { get; init; }

    public int BackfillTriageEnqueued { get; init; }

    public int BackfillAnalyticsRecorded { get; init; }

    public int BackfillSkippedDuplicate { get; init; }

    public string? BackfillError { get; init; }

    public bool BackfillIsScrapeOnly { get; init; }

    public string BackfillSummary { get; init; } = string.Empty;
}

public static class DashboardDataHealthHelper
{
    public static IReadOnlyList<DashboardInstanceHealthChip> BuildProfessionalHealthChips(
        IEnumerable<MessengerInstance> professionalInstances,
        MessageTriageService? triageService = null)
    {
        var service = triageService ?? MessageTriageService.Instance;
        var triageByInstance = service.GetAllItems()
            .GroupBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(instance =>
            {
                var health = AdapterHealthMonitor.Instance.GetStatus(instance.Id).State;
                triageByInstance.TryGetValue(instance.Id, out var triageCount);
                var backfillState = BackfillSyncManager.Instance.GetState(instance.Id);
                var lastResult = BackfillSyncManager.Instance.GetLastResult(instance.Id);

                return new DashboardInstanceHealthChip
                {
                    InstanceId = instance.Id,
                    DisplayName = instance.DisplayName,
                    Platform = PlatformDefinition.NormalizePlatformId(instance.Platform),
                    BackfillState = backfillState.ToString(),
                    AdapterHealth = health.ToString(),
                    TriageItemCount = triageCount,
                    BackfillTriageEnqueued = lastResult?.TriageEnqueued ?? 0,
                    BackfillAnalyticsRecorded = lastResult?.AnalyticsInboundRecorded ?? 0,
                    BackfillSkippedDuplicate = lastResult?.TriageSkippedDuplicate ?? 0,
                    BackfillError = lastResult?.ErrorMessage,
                    BackfillIsScrapeOnly = lastResult?.IsScrapeOnly == true,
                    BackfillSummary = BuildBackfillSummary(backfillState, lastResult)
                };
            })
            .ToList();
    }

    internal static string BuildBackfillSummary(BackfillSyncState state, BackfillResult? result)
    {
        if (result is null)
        {
            return string.Empty;
        }

        if (result.IsScrapeOnly)
        {
            return result.ScrapeOnlyReason ?? "Scrape-only refresh";
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return result.ErrorMessage;
        }

        if (state is BackfillSyncState.Completed or BackfillSyncState.Failed)
        {
            return $"{result.TriageEnqueued} triage · {result.AnalyticsInboundRecorded} analytics · {result.TriageSkippedDuplicate} skipped";
        }

        return string.Empty;
    }
}
