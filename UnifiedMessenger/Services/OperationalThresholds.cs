using System.Linq;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Shared operational timing thresholds for thread registry, dashboard, and analytics.
/// </summary>
public static class OperationalThresholds
{
    public const int DefaultRevenueLeakageMinutes = 30;

    public static double GetSlaThresholdMinutes() =>
        Math.Clamp(
            AppSettingsService.Instance.Settings.SlaThresholdMinutes,
            AppSettings.MinSlaThresholdMinutes,
            AppSettings.MaxSlaThresholdMinutes);

    /// <summary>
    /// SLA threshold for a specific location, honouring a per-location override when configured.
    /// Falls back to the global threshold when the location has no profile or no override.
    /// </summary>
    public static double GetSlaThresholdMinutes(string? locationKey)
    {
        if (FindProfile(locationKey)?.SlaThresholdMinutes is int minutes)
        {
            return Math.Clamp(minutes, AppSettings.MinSlaThresholdMinutes, AppSettings.MaxSlaThresholdMinutes);
        }

        return GetSlaThresholdMinutes();
    }

    /// <summary>Finds the workspace profile for a location key, or null when none is configured.</summary>
    public static WorkspaceProfile? FindProfile(string? locationKey)
    {
        if (string.IsNullOrWhiteSpace(locationKey))
        {
            return null;
        }

        var profiles = AppSettingsService.Instance.Settings.WorkspaceProfiles;
        return profiles?.FirstOrDefault(p =>
            string.Equals(p.LocationKey, locationKey, StringComparison.OrdinalIgnoreCase));
    }

    public static double GetRevenueLeakageMinutes() =>
        Math.Max(GetSlaThresholdMinutes() * 2, DefaultRevenueLeakageMinutes);

    public static double GetBranchLatencyGreenMaxMinutes() => GetSlaThresholdMinutes();

    public static double GetBranchLatencyAmberMaxMinutes() => GetRevenueLeakageMinutes();
}
