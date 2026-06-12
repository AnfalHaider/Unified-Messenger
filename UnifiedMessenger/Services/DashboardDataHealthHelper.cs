using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class DashboardInstanceHealthChip
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string Platform { get; init; }

    public required string AdapterHealth { get; init; }

    public int TriageItemCount { get; init; }
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

                return new DashboardInstanceHealthChip
                {
                    InstanceId = instance.Id,
                    DisplayName = instance.DisplayName,
                    Platform = PlatformDefinition.NormalizePlatformId(instance.Platform),
                    AdapterHealth = health.ToString(),
                    TriageItemCount = triageCount
                };
            })
            .ToList();
    }
}
