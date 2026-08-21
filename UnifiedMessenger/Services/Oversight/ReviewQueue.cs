namespace UnifiedMessenger.Services;

/// <summary>
/// How badly a review needs an answer. Ordered worst-first — the enum's own order is the queue's order.
/// </summary>
public enum ReviewUrgency
{
    /// <summary>One or two stars. An unhappy customer, in public, still unanswered.</summary>
    Critical,

    /// <summary>Three stars — a real complaint underneath a polite number.</summary>
    Elevated,

    /// <summary>The star rating could not be read, so this could be anything.</summary>
    Unrated,

    /// <summary>Four or five stars. Worth thanking, once the rest are handled.</summary>
    Routine
}

/// <summary>One review waiting for a reply, with the account it belongs to attached.</summary>
public readonly record struct QueuedReview(
    string InstanceId,
    string AccountName,
    string Reviewer,
    string Text,
    int Stars,
    string Age,
    int Index)
{
    public ReviewUrgency Urgency => ReviewQueue.UrgencyOf(Stars);
}

/// <summary>
/// Turns per-account review health into one answer-this-first queue across every location.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Reviews were only ever shown grouped under the account they came from, so an
/// owner with three salons had three separate lists and no way to see that the angriest unanswered review
/// in the business was the 1-star sitting at the bottom of the second one. Ordering is the product here;
/// the list is just how it is displayed.
/// </para>
/// <para>
/// <b>The order, and why.</b> Worst rating first, then longest-waiting within that. A one-star that has
/// been public for three weeks is the most expensive thing in the queue, and a five-star from this morning
/// is the least, however nice it would be to answer it.
/// </para>
/// </remarks>
public static class ReviewQueue
{
    /// <summary>
    /// Maps a star count to its bucket.
    /// </summary>
    /// <remarks>
    /// Anything outside 1-5 is <see cref="ReviewUrgency.Unrated"/> rather than being forced into a bucket.
    /// The scrape does not always recover the stars, and a review whose rating we failed to read is placed
    /// <i>below</i> known complaints but <i>above</i> known-good ones: most reviews are four or five stars,
    /// so treating an unread one as critical would push real complaints down the list — but it might still
    /// be a one-star, so it does not belong under reviews we know are positive.
    /// </remarks>
    public static ReviewUrgency UrgencyOf(int stars) => stars switch
    {
        1 or 2 => ReviewUrgency.Critical,
        3 => ReviewUrgency.Elevated,
        4 or 5 => ReviewUrgency.Routine,
        _ => ReviewUrgency.Unrated
    };

    /// <summary>Short label for the urgency chip.</summary>
    public static string Label(ReviewUrgency urgency) => urgency switch
    {
        ReviewUrgency.Critical => "Unhappy",
        ReviewUrgency.Elevated => "Mixed",
        ReviewUrgency.Unrated => "Rating unread",
        _ => "Positive"
    };

    /// <summary>
    /// Builds the ranked queue from each account's pending reviews.
    /// </summary>
    /// <param name="accounts">Account id, display name, and that account's health snapshot.</param>
    /// <remarks>
    /// Accounts with no data are skipped rather than contributing an empty group: a location the scrape has
    /// not managed to read yet is not the same as a location with nothing waiting, and the queue must not
    /// let the first look like the second.
    /// </remarks>
    public static IReadOnlyList<QueuedReview> Build(
        IEnumerable<(string InstanceId, string AccountName, GoogleReviewSnapshotService.ReviewHealth? Health)> accounts)
    {
        var queued = new List<QueuedReview>();

        foreach (var (instanceId, accountName, health) in accounts)
        {
            if (health is not { HasData: true } snapshot || snapshot.Pending is not { Count: > 0 } pending)
            {
                continue;
            }

            foreach (var review in pending)
            {
                queued.Add(new QueuedReview(
                    instanceId,
                    accountName,
                    review.Reviewer,
                    review.Text,
                    review.Stars,
                    review.Age,
                    review.Index));
            }
        }

        return queued
            .OrderBy(review => (int)review.Urgency)
            // Longest wait first. ReviewAge.SortKey yields TimeSpan.MinValue for an age it could not parse,
            // so those settle at the end of their own bucket instead of jumping the queue on a null.
            .ThenByDescending(review => ReviewAge.SortKey(review.Age))
            // Stable, human-predictable tie-break so the list does not reshuffle between renders.
            .ThenBy(review => review.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(review => review.Index)
            .ToList();
    }

    /// <summary>
    /// A one-line summary of what is waiting, for the desk header.
    /// </summary>
    /// <param name="anyAccountRead">
    /// Whether at least one account has actually been read. An empty queue means nothing without this.
    /// </param>
    /// <remarks>
    /// <para>
    /// Leads with the count that changes behaviour. "12 waiting" tells an owner nothing about whether to
    /// deal with it now; "3 unhappy" does.
    /// </para>
    /// <para>
    /// <b><paramref name="anyAccountRead"/> is required, not optional.</b> An empty queue is produced both
    /// by a business that has answered everything and by a scrape that has read nothing at all, and those
    /// must never render the same. Caught on screen: the header said "Nothing waiting for a reply" directly
    /// above a line saying reviews had not been read yet. A scrape that silently stops working would
    /// otherwise report itself as a clean queue forever, which is the worst failure this surface can have.
    /// </para>
    /// </remarks>
    public static string Summarise(IReadOnlyList<QueuedReview> queue, bool anyAccountRead)
    {
        if (queue.Count == 0)
        {
            return anyAccountRead
                ? "Nothing waiting for a reply."
                : "Not read yet — the app checks in the background.";
        }

        var critical = queue.Count(review => review.Urgency == ReviewUrgency.Critical);
        var waiting = queue.Count == 1 ? "1 review waiting" : $"{queue.Count} reviews waiting";

        return critical == 0
            ? waiting
            : $"{waiting} · {critical} from unhappy customers";
    }
}
