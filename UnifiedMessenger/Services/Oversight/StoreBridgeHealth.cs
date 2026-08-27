using System.Collections.Concurrent;

namespace UnifiedMessenger.Services;

/// <summary>
/// Tracks whether the WhatsApp in-memory store bridge is actually working, per instance. The bridge
/// depends on WhatsApp Web's internal module layout, which can change without warning; when it stops
/// resolving we silently fall back to the IndexedDB scan, so without this the degradation would be
/// invisible. Settings surfaces it as a health line ("Store bridge: active on 3 of 3 accounts").
/// </summary>
public static class StoreBridgeHealth
{
    public readonly record struct Entry(
        bool Succeeded,
        string Stage,
        string Strategy,
        int Conversations,
        int WithPreview,
        DateTimeOffset AtUtc);

    private static readonly ConcurrentDictionary<string, Entry> Entries =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Record(string instanceId, Entry entry)
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

    /// <summary>Accounts where the bridge produced usable data on its most recent attempt.</summary>
    public static int ActiveCount => Entries.Values.Count(e => e.Succeeded);

    /// <summary>Accounts the bridge has been attempted on at all.</summary>
    public static int AttemptedCount => Entries.Count;

    public static DateTimeOffset? LastSuccessUtc
    {
        get
        {
            var successes = Entries.Values.Where(e => e.Succeeded).ToList();
            return successes.Count == 0 ? null : successes.Max(e => e.AtUtc);
        }
    }

    /// <summary>One-line summary for the Settings health row.</summary>
    public static string Describe()
    {
        if (AttemptedCount == 0)
        {
            return "Not yet probed — sync an account to check.";
        }

        var active = ActiveCount;
        if (active == 0)
        {
            var stage = Entries.Values
                .OrderByDescending(e => e.AtUtc)
                .Select(e => e.Stage)
                .FirstOrDefault();
            return $"Unavailable ({stage ?? "unknown"}) — using the IndexedDB reader instead.";
        }

        var lastSuccess = LastSuccessUtc;
        var when = lastSuccess is null ? "" : $", last read {lastSuccess.Value.ToLocalTime():t}";
        return $"Active on {active} of {AttemptedCount} account{(AttemptedCount == 1 ? "" : "s")}{when}.";
    }

    /// <summary>
    /// True when at least one account has been probed and is running on the IndexedDB fallback.
    /// </summary>
    /// <remarks>
    /// Matters beyond preview coverage: the IndexedDB scan has no decrypted message model, so it cannot
    /// read WhatsApp's own <c>callOutcome</c> and emits an empty one. An empty outcome deliberately keeps
    /// a call counted — unknown must never close a chat — which means an already-ANSWERED inbound call is
    /// still queued as "missed · call back" on that path. Correct by design and over-stated in fact, so
    /// any surface showing a missed-call count has to say which it is.
    /// </remarks>
    public static bool AnyAccountOnFallback => AttemptedCount > 0 && ActiveCount < AttemptedCount;

    internal static void Reset() => Entries.Clear();
}
