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
/// <para><b>Five levels since A13.</b> Instagram forced the split: it lists every waiting customer by name
/// with an exact timestamp and cannot show what they said. Under the old classification that was
/// <see cref="ChannelCoverageLevel.CountsOnly"/>, which would have rendered a bare number and hidden a
/// list the app actually has. <see cref="ChannelCoverageLevel.NoMessageText"/> is the narrower, truer
/// statement, and <see cref="ChannelCoverageLevel.CountsOnly"/> now means what its name says: a count with
/// no list behind it at all.</para>
/// <para><see cref="ChannelCoverageLevel.CountsOnly"/> still has no live consumer, and that is the correct
/// outcome rather than a disappointment — it stays the documented rendering path for
/// <see cref="PlatformCapabilities.IsAggregateOnly"/> should a channel ever land there.</para>
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

    /// <summary>
    /// Every waiting conversation is listed with who and when, but not what they said. Narrower than
    /// <see cref="CountsOnly"/>, which has no list at all — Instagram is the first channel here.
    /// </summary>
    NoMessageText,

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

        // Not the WhatsApp pipeline gate. That gate answers "does this channel use WhatsApp IndexedDB",
        // which was the same question as "is it measured" only while WhatsApp was the only measured
        // channel. Instagram (A13) reads its own store and is measured without joining that pipeline, so
        // asking the pipeline would have reported it as an unread gap while its rows sat in the queue.
        if (!capabilities.ContributesConversationMetrics)
        {
            return ChannelCoverageLevel.NotMeasured;
        }

        return capabilities.CanReadPreview
            ? ChannelCoverageLevel.FullDetail
            : ChannelCoverageLevel.NoMessageText;
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

        var hasNoText = all.Any(i => For(i) is ChannelCoverageLevel.NoMessageText);
        if (counts.Count == 0 && countsOnly.Count == 0 && !hasNoText)
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

        // Deliberately last, and deliberately softer. These accounts ARE in the queue with every waiting
        // customer named; only the message text is missing. Leading with them would put the mildest gap
        // first and push the channels that are absent entirely to the end of a sentence nobody finishes.
        var noText = all
            .Where(i => For(i) is ChannelCoverageLevel.NoMessageText)
            .GroupBy(i => ChannelScope.ChannelName(i), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (noText.Count > 0)
        {
            var names = noText.Select(g => Describe(g.Count(), g.Key)).ToList();
            parts.Add($"{JoinReadable(names)} listed without message text — open a conversation to read what was said.");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// The two-or-three-word label for a card chip, so an account states its own coverage where its
    /// figures are, rather than only in a notice further down the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vocabulary lives here and not in the panel because the same words have to appear on the card, in
    /// the queue's branch header and in the leaderboard. Three surfaces inventing three phrasings for one
    /// idea is how "not shown", "unavailable" and "no data" ended up meaning the same thing in different
    /// places, each reading as a different problem.
    /// </para>
    /// <para>
    /// Only meaningful for a single account. A location rolling up WhatsApp, Instagram and Google has no
    /// one coverage level, and stamping any of these on it would be false — callers must render this for
    /// <see cref="OversightEntityKind.Instance"/> only.
    /// </para>
    /// </remarks>
    public static string ChipLabel(ChannelCoverageLevel level) => level switch
    {
        ChannelCoverageLevel.FullDetail => "Measured in full",
        ChannelCoverageLevel.CountsOnly => "Counts only",
        ChannelCoverageLevel.NoMessageText => "No message text",
        ChannelCoverageLevel.NotMeasured => "Not measured",
        ChannelCoverageLevel.NotAConversationChannel => "Reviews only",
        _ => string.Empty
    };

    /// <summary>
    /// The sentence behind <see cref="ChipLabel"/> — what the chip means, in the owner's terms.
    /// </summary>
    public static string ChipTooltip(ChannelCoverageLevel level) => level switch
    {
        ChannelCoverageLevel.FullDetail =>
            "Every waiting conversation on this account appears in the queue, with who is waiting and what they said.",
        ChannelCoverageLevel.CountsOnly =>
            "This account can be counted but not listed. Open it to see who is waiting and what they said.",
        ChannelCoverageLevel.NoMessageText =>
            "Every waiting customer is listed with how long they have waited. What they said stays in the app they sent it from — open the conversation to read it.",
        ChannelCoverageLevel.NotMeasured =>
            "Nothing reads this channel yet, so it contributes no figures here.",
        ChannelCoverageLevel.NotAConversationChannel =>
            "Not a conversation channel. Reviews and questions only — Google shut its message channel down in 2024.",
        _ => string.Empty
    };

    /// <summary>
    /// True when the chip is worth drawing at all.
    /// </summary>
    /// <remarks>
    /// <see cref="ChannelCoverageLevel.FullDetail"/> is the norm on this dashboard, and a chip on every
    /// card is decoration rather than signal — the same reasoning that keeps the session-state chip off a
    /// healthy account. The chip appears where something is genuinely missing.
    /// </remarks>
    public static bool ShouldShowChip(ChannelCoverageLevel level) =>
        level is not ChannelCoverageLevel.FullDetail;

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
