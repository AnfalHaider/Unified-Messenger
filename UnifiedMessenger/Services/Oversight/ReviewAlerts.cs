namespace UnifiedMessenger.Services;

/// <summary>
/// Decides which unhappy reviews are new enough to interrupt the owner for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure this is built around is over-notifying, not under-notifying.</b> A missed toast costs a
/// few minutes — the review is still at the top of the desk. A wrong toast trains the owner to dismiss them,
/// and after that the feature is worse than absent. Every rule below errs towards silence.
/// </para>
/// <para>
/// <b>Seeding.</b> The first time the app ever sees this account's reviews it alerts on nothing and records
/// everything. Without that, installing the app on a salon with five unanswered one-stars fires five
/// notifications about reviews that are weeks old, which is both useless and alarming.
/// </para>
/// </remarks>
public static class ReviewAlerts
{
    /// <summary>
    /// Identity for a review, stable across scrapes.
    /// </summary>
    /// <remarks>
    /// Deliberately excludes the age, which changes on every pass ("2 days ago" becomes "3 days ago") and
    /// would make every review look new every day. Excludes the queue index too, which shifts as reviews are
    /// answered. Two different reviews by the same name at the same rating collide and the second produces
    /// no toast — a missed alert, which is the direction to fail in.
    /// </remarks>
    public static string KeyFor(QueuedReview review) =>
        $"{review.InstanceId}|{review.Reviewer.Trim().ToLowerInvariant()}|{review.Stars}";

    /// <summary>
    /// Which of the current unhappy reviews have not been seen before.
    /// </summary>
    /// <param name="seen">Keys already recorded. Never pruned — see remarks.</param>
    /// <param name="seeded">
    /// False on the very first observation for this installation, when everything is recorded silently.
    /// </param>
    /// <returns>The reviews to alert on, and the keys to persist.</returns>
    /// <remarks>
    /// <b>Keys are never removed once recorded.</b> Pruning keys that are no longer in the queue looks tidy
    /// and is a bug: a single failed scrape returns an empty list, and the next successful one would then
    /// present every existing review as new and fire a burst of toasts about reviews the owner has already
    /// seen. Growth is bounded by how many reviews a business ever receives, which is small.
    /// </remarks>
    public static (IReadOnlyList<QueuedReview> ToAlert, IReadOnlyCollection<string> Seen) Evaluate(
        IEnumerable<QueuedReview> currentQueue,
        ISet<string> seen,
        bool seeded)
    {
        var unhappy = currentQueue
            .Where(review => review.Urgency == ReviewUrgency.Critical)
            .ToList();

        var updated = new HashSet<string>(seen, StringComparer.Ordinal);
        var fresh = new List<QueuedReview>();

        foreach (var review in unhappy)
        {
            var key = KeyFor(review);
            if (!updated.Add(key))
            {
                continue;
            }

            if (seeded)
            {
                fresh.Add(review);
            }
        }

        // Worst first, then longest waiting — the same order the desk itself uses, so the toast and the top
        // of the list always agree about what matters most.
        fresh = fresh
            .OrderBy(review => review.Stars)
            .ThenByDescending(review => ReviewAge.SortKey(review.Age))
            .ToList();

        return (fresh, updated);
    }

    /// <summary>
    /// The toast for a batch of newly-seen unhappy reviews.
    /// </summary>
    /// <remarks>
    /// One notification however many arrived. Three toasts for three reviews is how a useful signal becomes
    /// noise the owner turns off.
    /// </remarks>
    public static (string Title, string Body)? BuildToast(IReadOnlyList<QueuedReview> fresh)
    {
        if (fresh.Count == 0)
        {
            return null;
        }

        var worst = fresh[0];
        var who = string.IsNullOrWhiteSpace(worst.Reviewer) ? "A customer" : worst.Reviewer;
        var stars = worst.Stars == 1 ? "one-star" : "two-star";

        var title = fresh.Count == 1
            ? $"New {stars} review"
            : $"{fresh.Count} new unhappy reviews";

        var body = fresh.Count == 1
            ? $"{who} at {worst.AccountName}. It needs a reply."
            : $"Worst is {who} at {worst.AccountName}, {stars}. They need replies.";

        return (title, body);
    }
}
