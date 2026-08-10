using System.Collections.Concurrent;

namespace UnifiedMessenger.Services;

/// <summary>
/// Whether the app could actually read each account on its most recent attempt.
/// </summary>
/// <remarks>
/// Distinct from <see cref="StoreBridgeHealth"/> on purpose. That tracks whether the *preferred* in-memory
/// bridge resolved; a bridge failure that falls back to a working IndexedDB scan is a performance and
/// preview-quality story, not a data-loss one, and must not be reported as "can't read this account".
/// This records the *final* outcome of a whole refresh — after every fallback has been tried.
///
/// It exists because an account the app cannot read and an account that is simply quiet both produce zero
/// conversations, and therefore rendered identically ("no activity"). Those demand opposite responses from
/// the owner: one is good news, the other means oversight of that branch has stopped and customers may be
/// waiting unseen.
///
/// The signal is deliberately recorded, never inferred. Nothing here derives "failed" from a zero count —
/// a genuinely empty account must never be accused of being broken, because a false alarm here erodes
/// trust in exactly the direction the product cannot afford.
/// </remarks>
public static class AccountReadHealth
{
    public readonly record struct Entry(bool Succeeded, string Reason, DateTimeOffset AtUtc);

    private static readonly ConcurrentDictionary<string, Entry> Entries =
        new(StringComparer.OrdinalIgnoreCase);

    public static void RecordSuccess(string instanceId) =>
        Record(instanceId, new Entry(true, string.Empty, DateTimeOffset.UtcNow));

    public static void RecordFailure(string instanceId, string reason) =>
        Record(instanceId, new Entry(false, reason ?? string.Empty, DateTimeOffset.UtcNow));

    private static void Record(string instanceId, Entry entry)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        Entries[instanceId.Trim()] = entry;
    }

    public static Entry? TryGet(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && Entries.TryGetValue(instanceId.Trim(), out var entry)
            ? entry
            : null;

    /// <summary>
    /// True only when a read was attempted and failed.
    /// </summary>
    /// <remarks>
    /// An account that has never been read returns <c>false</c>. At startup, before the first scan
    /// completes, every account is in that state — claiming "can't read this account" then would fire the
    /// warning on every launch and train the owner to ignore it.
    /// </remarks>
    public static bool LastReadFailed(string instanceId) =>
        TryGet(instanceId) is { Succeeded: false };

    /// <summary>Clears recorded state. Test seam.</summary>
    internal static void Reset() => Entries.Clear();
}
