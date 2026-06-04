using UnifiedMessenger.Models;
using UnifiedMessenger.Pages;

namespace UnifiedMessenger.Services;

public static class SettingsPageHelper
{
    public const string AppDataFolderName = "UnifiedMessenger";

    public const string InstancesFileName = "instances.json";

    public static string FormatVersion(Version? version) =>
        version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";

    public static string BuildVersionLabel(Version? version) =>
        $"Unified Messenger {FormatVersion(version)}";

    public static string BuildUpdateCheckMessage(UpdateCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Status switch
        {
            UpdateCheckStatus.UpToDate =>
                $"You are on the latest version ({FormatVersion(result.CurrentVersion)}).",
            UpdateCheckStatus.UpdateAvailable =>
                $"Version {FormatVersion(result.LatestVersion)} is available. You are on {FormatVersion(result.CurrentVersion)}.",
            _ => string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "Could not check for updates. Try again later."
                : result.ErrorMessage
        };
    }

    public static int NormalizeMaxConcurrentWebViews(double rawValue) =>
        (int)Math.Clamp(Math.Round(rawValue), 0, AppSettings.MaxConcurrentWebViewsCap);

    public static int NormalizeSlaThresholdMinutes(double rawValue) =>
        (int)Math.Clamp(
            Math.Round(rawValue),
            AppSettings.MinSlaThresholdMinutes,
            AppSettings.MaxSlaThresholdMinutes);

    public static bool RequiresNumberBoxCorrection(double normalizedValue, double rawValue) =>
        Math.Abs(normalizedValue - rawValue) > 0.01;

    public static string GetDefaultAppDataRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName);

    public static string ResolveInstancesStorePath(string? registryStorePath) =>
        string.IsNullOrWhiteSpace(registryStorePath)
            ? Path.Combine(GetDefaultAppDataRoot(), InstancesFileName)
            : registryStorePath;

    public static IReadOnlyList<ArchivedAccountItem> BuildArchivedAccountItems(
        IEnumerable<MessengerInstance> archivedInstances)
    {
        ArgumentNullException.ThrowIfNull(archivedInstances);

        return archivedInstances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(instance => new ArchivedAccountItem(
                instance.Id.Trim(),
                instance.DisplayName,
                PlatformDefinition.FindById(instance.Platform)?.DisplayName ?? instance.Platform))
            .ToList();
    }

    public static string BuildImportSuccessMessage(int activeCount, int archivedCount) =>
        $"Loaded {activeCount} active and {archivedCount} archived instances.";
}
