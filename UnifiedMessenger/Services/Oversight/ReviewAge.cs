using System.Globalization;
using System.Text.RegularExpressions;

namespace UnifiedMessenger.Services;

/// <summary>
/// Turns Google's relative review age — "2 days ago", "a week ago" — into something the app can sort and
/// threshold on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The scrape captures <c>Age</c> as the literal string Google renders. A string
/// cannot answer "which review has been waiting longest", "is this one over three days old", or "how did
/// our reply time change this month" — and those are the whole difference between a count and a queue.
/// Google never exposes an absolute review date on the manager page, so a parsed approximation is the only
/// option available without the API.
/// </para>
/// <para>
/// <b>It is deliberately approximate, and the type says so.</b> "3 months ago" could be anywhere in a
/// two-week band, and a month is taken as 30 days. That precision is ample for ordering a reply queue and
/// for an "unanswered for N days" badge; it is <b>not</b> good enough to publish as a review's date, and
/// nothing should present it as one. When Google gives no age at all, this returns null rather than
/// guessing a time — an unknown age must never sort as though it were brand new.
/// </para>
/// </remarks>
public static class ReviewAge
{
    private static readonly Regex Pattern = new(
        @"(?<count>\d+|an?)\s*(?<unit>minute|min|hour|hr|day|week|month|year)s?\s*ago",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// How long ago the review was left, or null when Google gave nothing parseable.
    /// </summary>
    public static TimeSpan? Parse(string? age)
    {
        var text = (age ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        // Google uses these instead of a number for the most recent reviews.
        if (text.Contains("just now", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("moments ago", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.Zero;
        }

        if (text.Contains("yesterday", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromDays(1);
        }

        if (text.Contains("today", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.Zero;
        }

        var match = Pattern.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var countText = match.Groups["count"].Value;

        // "a week ago" / "an hour ago" — Google's wording for exactly one.
        var count = countText.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                    countText.Equals("an", StringComparison.OrdinalIgnoreCase)
            ? 1
            : int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;

        if (count <= 0)
        {
            return null;
        }

        return match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "minute" or "min" => TimeSpan.FromMinutes(count),
            "hour" or "hr" => TimeSpan.FromHours(count),
            "day" => TimeSpan.FromDays(count),
            "week" => TimeSpan.FromDays(count * 7),
            "month" => TimeSpan.FromDays(count * 30),
            "year" => TimeSpan.FromDays(count * 365),
            _ => null
        };
    }

    /// <summary>
    /// The approximate moment the review was left, for sorting and for "waiting N days" badges.
    /// </summary>
    public static DateTimeOffset? ApproximateLeftAtUtc(string? age, DateTimeOffset nowUtc) =>
        Parse(age) is { } elapsed ? nowUtc - elapsed : null;

    /// <summary>
    /// A sort key that puts the longest-waiting review first and pushes unknown ages to the end.
    /// </summary>
    /// <remarks>
    /// Unknown last, not first. An unparsed age is an absence of information, and letting it lead the
    /// queue would push a genuinely old review down the list on the strength of a string this code simply
    /// did not recognise.
    /// </remarks>
    public static TimeSpan SortKey(string? age) => Parse(age) ?? TimeSpan.MinValue;

    /// <summary>
    /// Short label for a review's wait — "6d", "3w". Falls back to Google's own words when unparsed, so
    /// the owner still sees whatever Google said rather than a blank.
    /// </summary>
    public static string ShortLabel(string? age)
    {
        if (Parse(age) is not { } elapsed)
        {
            return (age ?? string.Empty).Trim();
        }

        var days = elapsed.TotalDays;
        return days switch
        {
            < 1 => elapsed.TotalHours < 1 ? "just now" : $"{(int)elapsed.TotalHours}h",
            < 7 => $"{(int)days}d",
            < 60 => $"{(int)(days / 7)}w",
            < 365 => $"{(int)(days / 30)}mo",
            _ => $"{(int)(days / 365)}y"
        };
    }
}
