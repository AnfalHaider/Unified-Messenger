using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Two distinct gates:
/// <list type="bullet">
/// <item><see cref="IsPlatformModuleEnabled"/> — WhatsApp family only. This is the "participates in
/// WhatsApp pipelines" gate (history backfill, the WhatsApp adapter, delivery-status UI, dashboard
/// analytics). Do NOT broaden it — embed channels must not be pulled into WhatsApp IndexedDB scans.</item>
/// <item><see cref="IsSidebarVisible"/> — every platform that "Add account" can create. Embed channels
/// (Google Business, Telegram, Messenger, generic URL) are addable and must appear in the sidebar so the
/// user can open them; gating the sidebar on WhatsApp-only made them addable-but-invisible.</item>
/// </list>
/// </summary>
public static class PlatformModuleSettingsHelper
{
    public static bool IsPlatformModuleEnabled(string? platformId) =>
        PlatformDefinition.NormalizePlatformId(platformId) is "whatsapp" or "whatsappbusiness";

    public static IEnumerable<MessengerInstance> FilterEnabledInstances(IEnumerable<MessengerInstance> instances) =>
        instances.Where(instance => IsPlatformModuleEnabled(instance.Platform));

    /// <summary>True for any registered, addable platform — the gate for sidebar visibility.</summary>
    public static bool IsSidebarVisible(string? platformId) =>
        PlatformDefinition.FindById(PlatformDefinition.NormalizePlatformId(platformId)) is not null;

    public static IEnumerable<MessengerInstance> FilterSidebarVisibleInstances(IEnumerable<MessengerInstance> instances) =>
        instances.Where(instance => IsSidebarVisible(instance.Platform));

    public static IReadOnlyList<PlatformDefinition> GetSelectablePlatforms(AppSettings settings) =>
        PlatformDefinition.All.ToList();

    public static void NormalizePlatformModules(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
    }
}
