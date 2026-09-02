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

    /// <summary>
    /// The platforms offered in the Add-account picker — <b>every registered platform</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Telegram, Meta Business Suite and Instagram used to be withheld here on the grounds that they had no
    /// scraper yet. That made sense for a personal tool with one owner who did not use them. It is wrong for
    /// software sold to businesses whose channel mix nobody here can predict: an embed-only tab is still a
    /// real feature — one window instead of five — and withholding it decides on the customer's behalf that
    /// a channel they use is not worth showing them.
    /// </para>
    /// <para>
    /// The honesty guarantee does not depend on hiding anything, and never did: every entry renders its
    /// <see cref="PlatformDefinition.Description"/> in the picker, and <c>PlatformDescriptionTests</c>
    /// enforces both directions — an unmeasured channel must say "No oversight metrics", and a measured one
    /// must not claim it is unmeasured. A customer reading this list is told exactly what each channel does
    /// today. That is the control; an allowlist was never doing that job.
    /// </para>
    /// <para>
    /// The <c>settings</c> parameter that used to sit here is gone. Its own note said to remove it the next
    /// time this method changed, and this is that time.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<PlatformDefinition> GetSelectablePlatforms() => PlatformDefinition.All;

    // NormalizePlatformModules was deleted here: it took AppSettings, null-checked it, and did nothing
    // else, with no caller anywhere. A method named "normalize" that normalizes nothing is worse than dead
    // code — it is a claim that something is being kept consistent.
}
