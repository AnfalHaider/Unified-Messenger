namespace UnifiedMessenger.Services;

/// <summary>
/// Instagram's public-activity counts — new comments, likes and follow requests — per account (A13b).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a separate store from the conversation snapshot.</b> They are different kinds of fact.
/// A conversation snapshot is a list of named people the owner can act on one at a time; this is an
/// aggregate that <i>clears when the notifications panel is opened</i>, whether or not anyone replied.
/// Folding the two together would let a number that resets on a glance contaminate a queue that must not.
/// </para>
/// <para>
/// <b>In memory only, deliberately.</b> There is nothing worth persisting: the value is meaningless the
/// moment the owner looks at Instagram, and a figure restored from disk on next launch would be a claim
/// about a state that has already gone. It also keeps this type off <c>ApplicationPaths</c> entirely, so
/// a test touching it cannot write into the owner's real user-data folder.
/// </para>
/// </remarks>
public sealed class InstagramActivityStore
{
    public static InstagramActivityStore Instance { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, Snapshot> _byInstance = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="Comments">
    /// New comments on the business's own posts. The one figure here with oversight value — an unanswered
    /// comment is a customer waiting in public, where everyone can see it go unanswered.
    /// </param>
    /// <param name="Likes">Vanity. Carried because it is free, and because omitting it would make the
    /// total not add up on screen.</param>
    /// <param name="Relationships">Follows and follow requests.</param>
    public readonly record struct Snapshot(
        int Comments,
        int Likes,
        int Relationships,
        DateTimeOffset CapturedAtUtc)
    {
        public int Total => Comments + Likes + Relationships;
    }

    public void Update(string instanceId, int comments, int likes, int relationships, DateTimeOffset capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            _byInstance[instanceId.Trim()] = new Snapshot(
                Math.Max(0, comments),
                Math.Max(0, likes),
                Math.Max(0, relationships),
                capturedAtUtc);
        }
    }

    public bool TryGet(string? instanceId, out Snapshot snapshot)
    {
        snapshot = default;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        lock (_gate)
        {
            return _byInstance.TryGetValue(instanceId!.Trim(), out snapshot);
        }
    }

    public void Remove(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            _byInstance.Remove(instanceId!.Trim());
        }
    }

    /// <summary>
    /// The totals across the given accounts, or null when none of them has ever been read.
    /// </summary>
    /// <remarks>
    /// Null rather than a zeroed snapshot, because the two mean opposite things: zero is "we looked and
    /// there is no new activity", null is "nothing here has been read". A card rendering zeroes for an
    /// account nothing has read is the same false calm the sign-in gate exists to prevent.
    /// </remarks>
    public Snapshot? SumFor(IEnumerable<string>? instanceIds)
    {
        if (instanceIds is null)
        {
            return null;
        }

        var comments = 0;
        var likes = 0;
        var relationships = 0;
        var newest = DateTimeOffset.MinValue;
        var any = false;

        foreach (var id in instanceIds)
        {
            if (!TryGet(id, out var snapshot))
            {
                continue;
            }

            any = true;
            comments += snapshot.Comments;
            likes += snapshot.Likes;
            relationships += snapshot.Relationships;
            if (snapshot.CapturedAtUtc > newest)
            {
                newest = snapshot.CapturedAtUtc;
            }
        }

        return any ? new Snapshot(comments, likes, relationships, newest) : null;
    }

    /// <summary>
    /// The sentence that must accompany the number, in the owner's terms.
    /// </summary>
    /// <remarks>
    /// The wording is the feature. "9 comments need a reply" would be a claim the data cannot support:
    /// the count is <i>unseen activity</i>, and it clears when the notifications panel is opened whether
    /// or not anyone replied. So it under-reports after a glance and must never be phrased as a to-do
    /// list. It also cannot say who commented or on which post — Instagram does not fetch that on the
    /// feed — so the card sends the owner to Instagram rather than pretending to a drill-down.
    /// </remarks>
    public static string DescribeCaveat() =>
        "New since you last opened Instagram's notifications. It clears when you look there, whether or "
        + "not you replied — and Instagram does not say who commented or on which post.";
}
