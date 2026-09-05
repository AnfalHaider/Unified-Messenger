using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// The sentence under an open account's client, answering <i>"is this account actually being read?"</i>
/// (mockup §09).
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes.</b> That question had no on-screen answer anywhere in the app. An owner could
/// watch a client for a minute with no way to tell whether the app was reading it, had stopped, or had
/// never started — and every failure mode this stream has fixed (a signed-out account, a broken selector,
/// a scan that never ran) is invisible from inside the client it affects.
/// </para>
/// <para>
/// <b>Ordered worst-first.</b> The states are mutually exclusive and reported in the order that matters:
/// signed out beats read-failed beats never-read beats reading, because each earlier state makes the later
/// ones meaningless. An account nobody is signed into has not "failed to read" — there is nothing to read.
/// </para>
/// </remarks>
public static class AccountReadStrip
{
    public enum ReadState
    {
        /// <summary>Nothing is signed in. Everything else is moot.</summary>
        SignedOut,

        /// <summary>The last read of this account failed outright.</summary>
        ReadFailed,

        /// <summary>Nothing has been read yet — a page that has not loaded, not a fault.</summary>
        NeverRead,

        /// <summary>Reading normally.</summary>
        Reading,

        /// <summary>This channel has no scraper at all, so there is nothing to report.</summary>
        NotMeasured
    }

    public readonly record struct Status(ReadState State, string Text);

    /// <summary>
    /// What the strip should say for <paramref name="instance"/>, or null when it should not appear.
    /// </summary>
    /// <param name="conversationCount">Conversations in the latest snapshot, if any.</param>
    /// <param name="awaitingCount">Of those, how many are waiting.</param>
    /// <param name="lastReadUtc">When the latest snapshot was captured, if ever.</param>
    /// <param name="nowUtc">Injected so the relative phrasing can be tested without waiting.</param>
    public static Status? Describe(
        MessengerInstance? instance,
        int conversationCount,
        int awaitingCount,
        DateTimeOffset? lastReadUtc,
        DateTimeOffset nowUtc)
    {
        if (instance is null || string.IsNullOrWhiteSpace(instance.Id))
        {
            return null;
        }

        var coverage = ChannelCoverage.For(instance);

        // Silent for channels that carry no conversations at all. A Google Business tab is not failing to
        // be read — reviews are read on their own surface, and a strip saying "not measured" under a
        // reviews page would read as a fault where there is none.
        if (coverage == ChannelCoverageLevel.NotAConversationChannel)
        {
            return null;
        }

        if (SignInGate.IsSignedOut(instance.Id))
        {
            return new Status(
                ReadState.SignedOut,
                "Not signed in, so nothing is being read from this account. Sign in here and the next read will pick it up.");
        }

        if (coverage == ChannelCoverageLevel.NotMeasured)
        {
            return new Status(
                ReadState.NotMeasured,
                "Nothing reads this channel yet, so it contributes no figures to your dashboard.");
        }

        if (AccountReadHealth.LastReadFailed(instance.Id))
        {
            return new Status(
                ReadState.ReadFailed,
                "The last attempt to read this account did not work, so its figures are out of date.");
        }

        if (lastReadUtc is null)
        {
            return new Status(
                ReadState.NeverRead,
                "Nothing has been read from this account yet. It is picked up automatically once the page has loaded.");
        }

        var conversations = conversationCount == 1 ? "1 conversation" : $"{conversationCount} conversations";
        var waiting = awaitingCount == 1 ? "1 waiting" : $"{awaitingCount} waiting";
        var ago = DescribeAge(nowUtc - lastReadUtc.Value);

        // The privacy sentence is not decoration. This strip sits inside a window showing a customer's
        // messages, which is precisely where an owner wonders what the app is taking — so it answers
        // there, next to the evidence, rather than in a settings page nobody opens.
        var text = $"Reading. {conversations}, {waiting}, last read {ago}.";
        if (coverage == ChannelCoverageLevel.NoMessageText)
        {
            text += " Message text is never copied out of this client.";
        }

        return new Status(ReadState.Reading, text);
    }

    private static string DescribeAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            // A snapshot stamped in the future means a clock change, not a fresh read. "just now" is the
            // honest reading of "we cannot tell how old this is" and never claims a stale figure is new.
            return "just now";
        }

        return age.TotalSeconds switch
        {
            < 90 => "just now",
            < 3600 => $"{(int)age.TotalMinutes} min ago",
            < 86400 => $"{(int)age.TotalHours}h ago",
            _ => $"{(int)age.TotalDays}d ago"
        };
    }

    /// <summary>The brush token for the state's pip, so colour and wording cannot disagree.</summary>
    public static string PipBrushKey(ReadState state) => state switch
    {
        ReadState.Reading => UmSemanticBrushes.StatusSuccessBrushKey,
        ReadState.ReadFailed => UmSemanticBrushes.StatusDangerBrushKey,
        ReadState.SignedOut => UmSemanticBrushes.StatusWarningBrushKey,
        _ => UmSemanticBrushes.StatusMutedBrushKey
    };
}
