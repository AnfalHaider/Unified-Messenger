using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// WhatsApp-only platform gate after core slim-down (no per-module toggles).
/// </summary>
public static class PlatformModuleSettingsHelper
{
    public static bool IsPlatformModuleEnabled(string? platformId) =>
        PlatformDefinition.NormalizePlatformId(platformId) is "whatsapp" or "whatsappbusiness";

    public static IEnumerable<MessengerInstance> FilterEnabledInstances(IEnumerable<MessengerInstance> instances) =>
        instances.Where(instance => IsPlatformModuleEnabled(instance.Platform));

    public static IReadOnlyList<PlatformDefinition> GetSelectablePlatforms(AppSettings settings) =>
        PlatformDefinition.All.ToList();

    public static void NormalizePlatformModules(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
    }
}
