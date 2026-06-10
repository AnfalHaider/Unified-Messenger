using UnifiedMessenger.Models;
using UnifiedMessenger.Services.PlatformModules;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Services;

public static class PlatformModuleSettingsHelper
{
    public static IEnumerable<MessengerInstance> FilterEnabledInstances(
        IEnumerable<MessengerInstance> instances,
        AppSettings? settings = null)
    {
        settings ??= AppSettingsService.Instance.Settings;
        return instances.Where(instance => IsPlatformEnabled(instance.Platform, settings));
    }

    public static IEnumerable<ThreadData> FilterEnabledPlatformThreads(
        IEnumerable<ThreadData> threads,
        AppSettings? settings = null)
    {
        settings ??= AppSettingsService.Instance.Settings;
        return threads.Where(thread => IsPlatformEnabled(thread.Platform, settings));
    }

    public static bool IsPlatformModuleEnabled(string? platformId) =>
        IsPlatformEnabled(platformId ?? string.Empty, AppSettingsService.Instance.Settings);

    public static bool IsPlatformModuleEnabled(string? platformId, AppSettings settings) =>
        IsPlatformEnabled(platformId ?? string.Empty, settings);

    public static IReadOnlyList<PlatformDefinition> GetSelectablePlatforms(AppSettings settings) =>
        PlatformDefinition.All
            .Where(platform => IsPlatformSelectable(platform.Id, settings))
            .ToList();

    public static bool IsPlatformSelectable(string platformId, AppSettings settings) =>
        platformId.Equals("generic", StringComparison.OrdinalIgnoreCase) ||
        IsPlatformEnabled(platformId, settings);

    public static bool IsPlatformEnabled(string platformId, AppSettings settings)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);
        if (normalized.Equals("generic", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var module = PlatformModuleCatalog.Find(normalized);
        if (module is null || !module.CanDisable)
        {
            return true;
        }

        var entry = settings.PlatformModules
            .FirstOrDefault(item => item.PlatformId.Equals(normalized, StringComparison.OrdinalIgnoreCase));

        return entry?.IsEnabled ?? true;
    }

    public static void NormalizePlatformModules(AppSettings settings)
    {
        settings.PlatformModules ??= [];

        var byId = settings.PlatformModules
            .Where(item => !string.IsNullOrWhiteSpace(item.PlatformId))
            .GroupBy(item => PlatformDefinition.NormalizePlatformId(item.PlatformId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var normalized = new List<PlatformModuleSetting>();
        foreach (var module in PlatformModuleCatalog.AllInstalled.Where(descriptor => descriptor.CanDisable))
        {
            normalized.Add(new PlatformModuleSetting
            {
                PlatformId = module.PlatformId,
                IsEnabled = byId.TryGetValue(module.PlatformId, out var existing)
                    ? existing.IsEnabled
                    : true
            });
        }

        settings.PlatformModules = normalized;
    }

    public static IReadOnlyList<PlatformModuleToggleRowViewModel> BuildToggleRows(AppSettings settings)
    {
        NormalizePlatformModules(settings);

        return settings.PlatformModules
            .Select(item =>
            {
                var descriptor = PlatformModuleCatalog.Find(item.PlatformId);
                var definition = PlatformDefinition.FindById(item.PlatformId);
                return new PlatformModuleToggleRowViewModel
                {
                    PlatformId = item.PlatformId,
                    DisplayName = definition?.DisplayName ?? descriptor?.DisplayName ?? item.PlatformId,
                    IsEnabled = item.IsEnabled,
                    CanDisable = descriptor?.CanDisable ?? true,
                    CapabilitySummary = BuildCapabilitySummary(descriptor?.Capabilities ?? PlatformCapability.None)
                };
            })
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void SetPlatformEnabled(AppSettings settings, string platformId, bool isEnabled)
    {
        NormalizePlatformModules(settings);
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);
        var index = settings.PlatformModules.FindIndex(item =>
            item.PlatformId.Equals(normalized, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            settings.PlatformModules.Add(new PlatformModuleSetting
            {
                PlatformId = normalized,
                IsEnabled = isEnabled
            });
            return;
        }

        settings.PlatformModules[index].IsEnabled = isEnabled;
    }

    private static string BuildCapabilitySummary(PlatformCapability capabilities)
    {
        if (capabilities == PlatformCapability.None)
        {
            return "Notifications and badges";
        }

        var labels = new List<string>();
        if (capabilities.HasFlag(PlatformCapability.InboundTriage))
        {
            labels.Add("AI triage");
        }

        if (capabilities.HasFlag(PlatformCapability.AutoDraft))
        {
            labels.Add("Auto-draft");
        }

        if (capabilities.HasFlag(PlatformCapability.OperationalIntelligence))
        {
            labels.Add("Operations intelligence");
        }

        if (capabilities.HasFlag(PlatformCapability.DashboardScrape))
        {
            labels.Add("Dashboard scrape");
        }

        if (capabilities.HasFlag(PlatformCapability.Backfill))
        {
            labels.Add("Startup backfill");
        }

        if (labels.Count == 0)
        {
            return "Notifications and badges";
        }

        return string.Join(" · ", labels);
    }
}
