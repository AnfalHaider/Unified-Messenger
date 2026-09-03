using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// How much of a channel the "who is waiting" queue can actually show, and the sentence that says so.
/// </summary>
/// <remarks>
/// <para><b>The defect this closes.</b> The queue is built from the WhatsApp pipeline
/// (<see cref="PlatformModuleSettingsHelper.IsPlatformModuleEnabled"/>), so an owner with a Messenger
/// account connected sees a cross-account list of waiting customers that silently omits an entire channel
/// they receive customer messages on. Nothing on screen said so. That is the exact defect class the
/// v4.99.46–48 audits fixed three times: one noun spanning two populations, with nothing to mark the
/// difference — and it is worse here than on a chart, because the missing thing is a waiting customer.</para>
/// <para><b>Why four levels and not a bool.</b> "Not shown" collapses three genuinely different situations
/// that need different words. A Google Business account is not missing from a conversation queue — it has
/// no conversations, because Google Business Messages was shut down in 2024. A Messenger account is
/// missing, and the honest reason is that nothing has been built to read it yet. And a channel that can be
/// counted but not detailed is missing something narrower still. Telling an owner "1 channel not shown"
/// for all three would be true and useless.</para>
/// <para><see cref="ChannelCoverageLevel.CountsOnly"/> is the rendering path
/// <see cref="PlatformCapabilities.IsAggregateOnly"/> has documented since it was written and never had:
/// <i>"callers should render a count and explicitly say detail is unavailable, rather than showing an
/// empty list"</i>. No platform reaches it today — Meta declares <c>CanReadUnread = false</c> because no
/// adapter exists — so it is one branch of a function whose others ship live, waiting for the increment
/// that flips that flag rather than a surface built on speculation.</para>
/// </remarks>
public enum ChannelCoverageLevel
{
    /// <summary>Every waiting conversation appears in the queue, with who and what.</summary>
    FullDetail,

    /// <summary>
    /// A number is readable but per-conversation detail is not — either the platform forbids it, or the
    /// adapter cannot read a preview yet. Render the count and say detail is unavailable; never an empty list.
    /// </summary>
    CountsOnly,

    /// <summary>Carries customer conversations, and nothing here can read them yet. The honest gap.</summary>
    NotMeasured,

    /// <summary>Not a conversation channel at all, so its absence from a conversation queue is correct.</summary>
    NotAConversationChannel
}

public static class ChannelCoverage
{
    public static ChannelCoverageLevel For(MessengerInstance? instance) =>
        For(instance?.Platform);

    public static ChannelCoverageLevel For(string? platformId)
    {
        var capabilities = PlatformDefinition.CapabilitiesFor(platformId);

        if (!capabilities.IsMessageChannel)
        {
            return ChannelCoverageLevel.NotAConversationChannel;
        }

        if (capabilities.IsAggregateOnly)
        {
            return ChannelCoverageLevel.CountsOnly;
        }

        // The pipeline gate, not the capability flags: a channel can declare it reads previews and still
        // not be wired into the queue that renders them. What the owner sees is decided by the pipeline.
        return PlatformModuleSettingsHelper.IsPlatformModuleEnabled(platformId)
            ? ChannelCoverageLevel.FullDetail
            : ChannelCoverageLevel.NotMeasured;
    }

    /// <summary>
    /// One sentence for the queue, naming the channels whose waiting customers it cannot show. Empty when
    /// there is nothing to disclose — a notice that appears on every screen stops being read.
    /// </summary>
    /// <remarks>
    /// Deliberately silent about <see cref="ChannelCoverageLevel.NotAConversationChannel"/>. A Google
    /// Business account is not a gap in a conversation queue, and listing it as one would train the owner
    /// to dismiss the line that does matter.
    /// </remarks>
    public static string DescribeGaps(IEnumerable<MessengerInstance>? instances)
    {
        var all = (instances ?? []).Where(i => i is not null).ToList();
        if (all.Count == 0)
        {
            return string.Empty;
        }

        var counts = all
            .Where(i => For(i) is ChannelCoverageLevel.NotMeasured)
            .GroupBy(i => ChannelScope.ChannelName(i), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var countsOnly = all
            .Where(i => For(i) is ChannelCoverageLevel.CountsOnly)
            .GroupBy(i => ChannelScope.ChannelName(i), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (counts.Count == 0 && countsOnly.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        if (counts.Count > 0)
        {
            var names = counts.Select(g => Describe(g.Count(), g.Key)).ToList();
            parts.Add($"{JoinReadable(names)} not shown here — nothing reads {(counts.Count == 1 && counts[0].Count() == 1 ? "that channel" : "those channels")} yet.");
        }

        if (countsOnly.Count > 0)
        {
            var names = countsOnly.Select(g => Describe(g.Count(), g.Key)).ToList();
            parts.Add($"{JoinReadable(names)} can be counted but not listed — opening a conversation there would tell the customer you looked.");
        }

        return string.Join(" ", parts);
    }

    private static string Describe(int count, string channel) =>
        count == 1 ? $"1 {channel} account" : $"{count} {channel} accounts";

    private static string JoinReadable(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => string.Empty,
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + $" and {parts[^1]}"
    };
}
