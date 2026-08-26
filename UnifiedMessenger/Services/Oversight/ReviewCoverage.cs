namespace UnifiedMessenger.Services;

/// <summary>
/// Says how much of a profile's review history the scrape actually saw.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is its own type.</b> Google paginates the reviews manager, so
/// <see cref="GoogleReviewSnapshotService.ReviewHealth.Total"/> is "reviews on the page we loaded", not
/// "reviews this business has". The panel rendered that number bare, which reads as the latter. A business
/// with 239 reviews showing "40 · 88% replied" is being told something false about itself, and the reply
/// rate is over the loaded window rather than the profile.
/// </para>
/// <para>
/// The lifetime total comes from a different scrape entirely — the Google Search merchant view, throttled
/// to every six hours — so the two can disagree, and this handles that rather than pretending they can't.
/// </para>
/// </remarks>
public static class ReviewCoverage
{
    /// <summary>
    /// Coverage stated from what the traversal actually did, which beats inferring it from two numbers.
    /// </summary>
    /// <param name="reachedLastPage">
    /// True when the scrape clicked through to a page whose Next button was disabled.
    /// </param>
    /// <remarks>
    /// This overload is the one to prefer. <paramref name="reachedLastPage"/> is a fact about this scrape;
    /// the lifetime total is a separate six-hourly scrape that can be stale in either direction. When the
    /// traversal reached the end, the loaded count IS the profile — say so, and use it even if the cached
    /// lifetime total disagrees.
    /// </remarks>
    public static string Describe(int loaded, int? profileTotal, bool reachedLastPage)
    {
        if (reachedLastPage && loaded > 0)
        {
            return $"covers all {loaded:N0} reviews";
        }

        if (loaded > 0 && profileTotal is { } total && total > loaded)
        {
            // Known to be partial. Naming the stopping point matters: "the first 100" tells the owner the
            // reply rate below is over recent reviews, not their whole history.
            return $"covers the first {loaded:N0} of {total:N0}";
        }

        return Describe(loaded, profileTotal);
    }

    /// <summary>As above, for the reply rate's basis line.</summary>
    public static string DescribeReplyRateBasis(int loaded, int? profileTotal, bool reachedLastPage) =>
        reachedLastPage && loaded > 0
            ? "of all reviews"
            : DescribeReplyRateBasis(loaded, profileTotal);

    /// <summary>
    /// Whether the reply queue holds every unanswered review, or only a sample of them.
    /// </summary>
    /// <remarks>
    /// The scrape reads the reply-button COUNT for the whole page but builds preview text for only the
    /// first handful, because a preview costs a DOM expansion each. So the queue is a sample and
    /// <c>ReviewHealth.Unanswered</c> is the total, and they are routinely different numbers. The desk
    /// rendered the queue length as "Unanswered", which put "8" on the page beside a sidebar badge reading
    /// 45 and a basis line implying the same — a truncated sample presented as a total. Anything derived
    /// from the queue (oldest waiting, how many are at three stars or below) is a fact about the sample
    /// only, and has to say so.
    /// </remarks>
    public static bool QueueIsSample(int shown, int unanswered) => unanswered > shown;

    /// <summary>Suffix naming the sample a queue-derived figure was actually computed over.</summary>
    public static string DescribeQueueSample(int shown, int unanswered) =>
        QueueIsSample(shown, unanswered)
            ? shown == 1 ? "in the 1 loaded" : $"in the {shown:N0} loaded"
            : string.Empty;

    /// <summary>
    /// Whether the scrape saw every review the profile has. Null total means unknown, which is NOT complete.
    /// </summary>
    /// <remarks>
    /// Unknown counts as incomplete deliberately. Claiming full coverage on the strength of a number we
    /// never read would be exactly the kind of confident wrongness this whole tier exists to remove.
    /// </remarks>
    public static bool IsComplete(int loaded, int? profileTotal) =>
        profileTotal is { } total && total > 0 && loaded >= total;

    /// <summary>
    /// A plain-language coverage line for the section header.
    /// </summary>
    public static string Describe(int loaded, int? profileTotal)
    {
        if (loaded <= 0)
        {
            return profileTotal is { } t and > 0
                ? $"no reviews read yet of {t:N0}"
                : "no reviews read yet";
        }

        if (profileTotal is not { } total || total <= 0)
        {
            // The lifetime total is scraped separately and may not have run yet. Say what we did read
            // rather than implying it is everything.
            return loaded == 1 ? "covers 1 loaded review" : $"covers {loaded:N0} loaded reviews";
        }

        // The two scrapes are throttled independently, so a stale lifetime total can sit BELOW what the
        // reviews page just showed. Treat that as complete rather than rendering "covers 41 of 39", which
        // reads as a bug and undermines every other number on the page.
        if (loaded >= total)
        {
            return $"covers all {total:N0} reviews";
        }

        return $"covers the {loaded:N0} most recent of {total:N0}";
    }

    /// <summary>
    /// What the reply rate is actually a rate <i>of</i>, so the figure is never read as profile-wide when
    /// it is only over the loaded window.
    /// </summary>
    public static string DescribeReplyRateBasis(int loaded, int? profileTotal) =>
        IsComplete(loaded, profileTotal)
            ? "of all reviews"
            : $"of the {loaded:N0} most recent";
}
