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

    public static double GetRevenueLeakageMinutes() =>
        Math.Max(GetSlaThresholdMinutes() * 2, DefaultRevenueLeakageMinutes);

    public static double GetBranchLatencyGreenMaxMinutes() => GetSlaThresholdMinutes();

    public static double GetBranchLatencyAmberMaxMinutes() => GetRevenueLeakageMinutes();
}
