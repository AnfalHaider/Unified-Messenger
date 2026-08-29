using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Names the accounts a conversation-metric figure actually covers.
/// </summary>
/// <remarks>
/// <para>
/// Analytics and the business report both draw their numbers from the WhatsApp IndexedDB pipeline —
/// <see cref="PlatformModuleSettingsHelper.IsPlatformModuleEnabled"/> — and both presented the result as
/// covering every account the owner has. That is not a future problem: an owner with Google Business
/// accounts connected is already reading a chart built from a subset of the accounts its own subtitle
/// claims. It is the defect class the v4.99.46–47 audit fixed three times over: one noun spanning two
/// populations, with nothing on screen to say so.
/// </para>
/// <para>
/// One implementation, both callers, so the screen and the exported document cannot drift apart — the
/// mistake that put two different figures under the label "SLA met" on one page.
/// </para>
/// <para>
/// The sentence names what is <i>excluded</i> rather than what is included, because the excluded set is
/// the fact the owner does not already have. "Covers all 5 accounts" is worth saying too: it is what makes
/// the excluded case legible when it appears, instead of a line that only shows up on bad days.
/// </para>
/// </remarks>
public static class ChannelScope
{
    /// <summary>
    /// One sentence naming how many of these accounts contribute conversation metrics, and which channels
    /// do not. Empty string for an empty set — the caller's no-accounts empty state says that better than
    /// a scope line over nothing.
    /// </summary>
    public static string Describe(IEnumerable<MessengerInstance>? instances)
    {
        var all = (instances ?? []).Where(instance => instance is not null).ToList();
        if (all.Count == 0)
        {
            return string.Empty;
        }

        var excluded = all
            .Where(instance => !PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
            .ToList();
        var covered = all.Count - excluded.Count;

        if (excluded.Count == 0)
        {
            return covered == 1 ? "Covers your 1 account." : $"Covers all {covered} accounts.";
        }

        var names = excluded
            .GroupBy(ChannelName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Count()} {group.Key}")
            .ToList();

        return $"Covers {covered} of {all.Count} accounts — {JoinReadable(names)} not measured here.";
    }

    /// <summary>The channel's product name, for the report's per-account table.</summary>
    public static string ChannelName(MessengerInstance instance) =>
        PlatformDefinition.FindById(PlatformDefinition.NormalizePlatformId(instance?.Platform))?.DisplayName
        ?? "Other";

    private static string JoinReadable(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => string.Empty,
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}"
    };
}
