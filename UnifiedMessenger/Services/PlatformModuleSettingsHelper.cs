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
    /// <summary>
    /// Participates in the WhatsApp IndexedDB pipelines. Now derived from
    /// <see cref="PlatformCapabilities.UsesWhatsAppIndexedDbPipeline"/> rather than a hard-coded id list,
    /// but the semantics and the result are unchanged: WhatsApp family only. A new channel earns oversight
    /// metrics by declaring its own capabilities and shipping its own adapter -- never by being added here.
    /// </summary>
    public static bool IsPlatformModuleEnabled(string? platformId) =>
        PlatformDefinition.CapabilitiesFor(platformId).UsesWhatsAppIndexedDbPipeline;

    /// <summary>
    /// True when the channel contributes conversation metrics the command center can render today. Broader
    /// than <see cref="IsPlatformModuleEnabled"/> by design: a channel can contribute unread/awaiting
    /// without joining the WhatsApp pipelines. Use this for command-center inclusion; use
    /// <see cref="IsPlatformModuleEnabled"/> only for WhatsApp-specific plumbing.
    /// </summary>
    public static bool ContributesConversationMetrics(string? platformId) =>
        PlatformDefinition.CapabilitiesFor(platformId).ContributesConversationMetrics;

    public static IEnumerable<MessengerInstance> FilterConversationMetricInstances(
        IEnumerable<MessengerInstance> instances) =>
        instances.Where(instance => ContributesConversationMetrics(instance.Platform));

    public static IEnumerable<MessengerInstance> FilterEnabledInstances(IEnumerable<MessengerInstance> instances) =>
        instances.Where(instance => IsPlatformModuleEnabled(instance.Platform));

    /// <summary>True for any registered, addable platform — the gate for sidebar visibility.</summary>
    public static bool IsSidebarVisible(string? platformId) =>
        PlatformDefinition.FindById(PlatformDefinition.NormalizePlatformId(platformId)) is not null;

    public static IEnumerable<MessengerInstance> FilterSidebarVisibleInstances(IEnumerable<MessengerInstance> instances) =>
        instances.Where(instance => IsSidebarVisible(instance.Platform));

    // Platforms hidden from the "Add account" picker for now (no scraper, and Meta actively fights automation).
    // They stay in PlatformDefinition.All so existing accounts still resolve and the nav-guard allowlist keeps
    // their hosts — they're just not offered as new-account choices.
    private static readonly HashSet<string> HiddenFromPicker =
        new(StringComparer.OrdinalIgnoreCase) { "telegram", "metabusinesssuite", "instagram" };

    /// <summary>
    /// The platforms offered in the Add-account picker.
    /// </summary>
    /// <remarks>
    /// <paramref name="settings"/> is not read. It is kept because the hidden set was once expected to be
    /// user-configurable and the call sites already pass one; dropping it is a signature change across the
    /// two dialogs and their tests for no behaviour. If it is still unused when something else here
    /// changes, remove it then.
    /// </remarks>
    public static IReadOnlyList<PlatformDefinition> GetSelectablePlatforms(AppSettings settings) =>
        PlatformDefinition.All.Where(p => !HiddenFromPicker.Contains(p.Id)).ToList();

    // NormalizePlatformModules was deleted here: it took AppSettings, null-checked it, and did nothing
    // else, with no caller anywhere. A method named "normalize" that normalizes nothing is worse than dead
    // code — it is a claim that something is being kept consistent.
}
