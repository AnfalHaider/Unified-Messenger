namespace UnifiedMessenger.Services;

/// <summary>One day's reading for one Google account.</summary>
/// <remarks>
/// <paramref name="Rating"/> and <paramref name="LifetimeTotal"/> are nullable because they come from a
/// separate six-hourly scrape that can fail on its own; a day where only the reviews page was read still
/// records the answered/unanswered counts.
/// </remarks>
public readonly record struct ReviewDayPoint(
    DateOnly Day,
    double? Rating,
    int? LifetimeTotal,
    int Unanswered,
    int Answered);

/// <summary>
/// What changed over time, derived from stored daily readings.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why velocity comes from the lifetime total.</b> "14 new this month" would normally need a date on every
/// review, which the scrape does not have — it reads a relative age for pending reviews only. But the profile's
/// lifetime total is scraped every six hours, and the difference between two readings of it IS the number of
/// reviews gained, for every review, answered or not. That one substitution is what makes this tier buildable
/// without new scraping.
/// </para>
/// <para>
/// <b>Every method returns the window it actually covered.</b> With four days of history, "gained in the last
/// 30 days" is not a statement anyone can make. The caller gets the real span and renders that, so the page
/// says "+2 in 4 days" rather than quietly implying a month.
/// </para>
/// </remarks>
public static class ReviewTrend
{
    /// <summary>A change measured over the span that was actually available.</summary>
    /// <param name="OverDays">
    /// Days between the two readings used. Always the truth, never the requested window.
    /// </param>
    public readonly record struct Change<T>(T From, T To, int OverDays) where T : struct;

    /// <summary>
    /// Rating at the start and end of the window, or null when fewer than two readings carry one.
    /// </summary>
    /// <remarks>
    /// Two <i>distinct</i> readings are required. A single day repeated would report a change of zero, which
    /// is a claim about stability that one measurement cannot support.
    /// </remarks>
    public static Change<double>? RatingChange(IReadOnlyList<ReviewDayPoint> points, int days)
    {
        var window = Window(points, days).Where(p => p.Rating is not null).ToList();
        if (window.Count < 2)
        {
            return null;
        }

        var first = window[0];
        var last = window[^1];
        if (first.Day == last.Day)
        {
            return null;
        }

        return new Change<double>(first.Rating!.Value, last.Rating!.Value, last.Day.DayNumber - first.Day.DayNumber);
    }

    /// <summary>
    /// Reviews gained across the window — the lifetime total's increase between the first and last reading.
    /// </summary>
    public static Change<int>? ReviewsGained(IReadOnlyList<ReviewDayPoint> points, int days)
    {
        var window = Window(points, days).Where(p => p.LifetimeTotal is not null).ToList();
        if (window.Count < 2)
        {
            return null;
        }

        var first = window[0];
        var last = window[^1];
        if (first.Day == last.Day)
        {
            return null;
        }

        return new Change<int>(first.LifetimeTotal!.Value, last.LifetimeTotal!.Value, last.Day.DayNumber - first.Day.DayNumber);
    }

    /// <summary>
    /// Days since the lifetime total last went up — how long this location has been quiet.
    /// </summary>
    /// <remarks>
    /// Null until there are two readings to compare, and null again if the total has risen on every reading
    /// we hold: in that case nothing has been quiet, and the honest answer is "not quiet", not "0 days".
    /// </remarks>
    public static int? DaysSinceNewReview(IReadOnlyList<ReviewDayPoint> points, DateOnly asOf)
    {
        var known = points.Where(p => p.LifetimeTotal is not null).OrderBy(p => p.Day).ToList();
        if (known.Count < 2)
        {
            return null;
        }

        for (var i = known.Count - 1; i > 0; i--)
        {
            if (known[i].LifetimeTotal > known[i - 1].LifetimeTotal)
            {
                return asOf.DayNumber - known[i].Day.DayNumber;
            }
        }

        // No increase anywhere in what we hold: quiet for at least the whole span, and we say "at least" by
        // measuring from the oldest reading rather than inventing a start date.
        return asOf.DayNumber - known[0].Day.DayNumber;
    }

    /// <summary>Ratings across the window, oldest first — the sparkline's values.</summary>
    public static IReadOnlyList<double> RatingSeries(IReadOnlyList<ReviewDayPoint> points, int days) =>
        Window(points, days).Where(p => p.Rating is not null).Select(p => p.Rating!.Value).ToList();

    /// <summary>
    /// Reply rate at the start and end of the window, in whole percent.
    /// </summary>
    public static Change<int>? ReplyRateChange(IReadOnlyList<ReviewDayPoint> points, int days)
    {
        var window = Window(points, days).Where(p => p.Unanswered + p.Answered > 0).ToList();
        if (window.Count < 2)
        {
            return null;
        }

        var first = window[0];
        var last = window[^1];
        if (first.Day == last.Day)
        {
            return null;
        }

        return new Change<int>(
            MetricMath.HonestPercent(first.Answered, first.Unanswered + first.Answered),
            MetricMath.HonestPercent(last.Answered, last.Unanswered + last.Answered),
            last.Day.DayNumber - first.Day.DayNumber);
    }

    /// <summary>
    /// Plain-language span for a measured change — "in 30 days", "in 4 days", "since yesterday".
    /// </summary>
    /// <remarks>
    /// Rendered next to every delta so a figure gathered over three days can never read as a monthly one.
    /// </remarks>
    public static string SpanLabel(int overDays) => overDays switch
    {
        <= 0 => "today",
        1 => "since yesterday",
        < 14 => $"in {overDays} days",
        < 60 => $"in {overDays / 7} weeks",
        _ => $"in {overDays / 30} months"
    };

    private static List<ReviewDayPoint> Window(IReadOnlyList<ReviewDayPoint> points, int days)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var newest = points.Max(p => p.Day);
        var cutoff = newest.AddDays(-Math.Max(0, days));
        return points.Where(p => p.Day >= cutoff).OrderBy(p => p.Day).ToList();
    }
}
