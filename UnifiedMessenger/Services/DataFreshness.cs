namespace UnifiedMessenger.Services;

/// <summary>How old the data behind a surface is, and whether that is worth flagging.</summary>
public readonly record struct FreshnessVerdict(string Text, bool IsStale, bool HasData)
{
    /// <summary>Nothing has ever been captured — say that rather than showing a bare "never".</summary>
    public static FreshnessVerdict NoData { get; } =
        new("No data captured yet — press Re-sync", IsStale: true, HasData: false);
}

/// <summary>
/// The single place that phrases "how old is this".
///
/// <para>
/// <b>Why it exists.</b> Only the command center and the review panel said when their numbers were
/// captured. Analytics, Reports and the dashboard rendered message counts, charts and response times with
/// no stamp at all — so a scrape that failed three hours ago looked exactly like one that succeeded thirty
/// seconds ago. Stale data presented as current is the most dangerous state a dashboard has, because it is
/// indistinguishable from good news and the owner acts on it.
/// </para>
/// <para>
/// Everything on those surfaces derives from the same oversight snapshot, so one stamp is truthful for all
/// of them — and putting the wording here means the three surfaces cannot drift into describing the same
/// staleness three different ways.
/// </para>
/// </summary>
public static class DataFreshness
{
    /// <summary>
    /// Past this, the numbers are flagged rather than merely stamped.
    /// </summary>
    /// <remarks>
    /// Thirty minutes, against a background poll that runs every 25–90 seconds. Anything beyond that means
    /// the poll has been failing rather than merely lagging, which is a different message: the figure is not
    /// slightly behind, it is unreliable.
    /// </remarks>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    /// <summary>The freshness of the oversight data every metric surface is built from.</summary>
    public static FreshnessVerdict Current(DateTimeOffset? nowUtc = null) =>
        Describe(OversightChatSnapshotService.Instance.LastCapturedUtc, nowUtc);

    /// <summary>Phrases a capture time. Pure, so the wording and the threshold are testable.</summary>
    public static FreshnessVerdict Describe(DateTimeOffset? capturedAtUtc, DateTimeOffset? nowUtc = null)
    {
        if (capturedAtUtc is not { } captured)
        {
            return FreshnessVerdict.NoData;
        }

        var age = (nowUtc ?? DateTimeOffset.UtcNow) - captured;

        // A clock change or a snapshot written by a machine slightly ahead can put the capture in the
        // future. "Updated in 3 minutes" reads as a bug and undermines every other number on the page.
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        var stale = age >= StaleAfter;
        var phrase = Phrase(age);

        return new FreshnessVerdict(
            stale ? $"Updated {phrase} — press Re-sync for current numbers" : $"Updated {phrase}",
            stale,
            HasData: true);
    }

    private static string Phrase(TimeSpan age) => age.TotalSeconds switch
    {
        < 60 => "just now",
        < 120 => "1 minute ago",
        < 3600 => $"{(int)age.TotalMinutes} minutes ago",
        < 7200 => "1 hour ago",
        < 86400 => $"{(int)age.TotalHours} hours ago",
        < 172800 => "yesterday",
        _ => $"{(int)age.TotalDays} days ago"
    };
}
