using System.Collections.Concurrent;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Whether each account's scraper is still finding the page elements it depends on — and, when it is not,
/// how far down its fallback list it has had to go.
/// </summary>
/// <remarks>
/// <para>The failure this exists to prevent: WhatsApp ships a markup change, every customer breaks on the
/// same day, and the vendor finds out from support tickets. The selector manifest gives each anchor an
/// ordered candidate list; the page records <i>which index matched</i>, and a rising index is the earliest
/// possible warning that a redesign is coming — it arrives while everything still works.</para>
/// <para><b>Distinct from the two health signals either side of it, and deliberately so:</b></para>
/// <list type="bullet">
/// <item><see cref="AccountReadHealth"/> — could we read this account <i>at all</i>, after every fallback.
/// That is the data-loss signal.</item>
/// <item><see cref="StoreBridgeHealth"/> — did the preferred in-memory reader resolve, or did we drop to
/// the IndexedDB scan. That is a preview-quality signal.</item>
/// <item>This — are the DOM anchors themselves still matching, and at which candidate. A degraded anchor
/// that still resolves is <b>not</b> a read failure and must never be reported as one: the numbers are
/// correct today, and crying wolf here would train the owner to ignore the one warning that matters.</item>
/// </list>
/// <para>Nothing here leaves the machine. This is the owner's screen and <c>app.log</c>, never a vendor
/// endpoint — the customers whose conversations these anchors read are not ours to report on.</para>
/// </remarks>
public static class SelectorHealth
{
    /// <summary>
    /// Consecutive scans an anchor must fail to match anywhere before it is called broken.
    /// </summary>
    /// <remarks>
    /// <para><b>This number is doing real work, and a smaller one is wrong.</b> WhatsApp Web serves a
    /// fully-loaded document with an empty chat list during a cold sync — measured 2026-09-02 flapping
    /// between 64 rows, 0 and 66 over roughly three minutes, with <c>readyState</c> "complete" throughout.
    /// In that window a perfectly healthy account reports exactly what a broken selector reports.</para>
    /// <para>The two cannot be told apart from inside the browser — that is the finding — so the only
    /// honest discriminator is <b>time</b>: a cold sync resolves, a stale selector never does. Against the
    /// adaptive 25s/90s scan cadence, ten consecutive misses is roughly four to fifteen minutes: well past
    /// any sync, and still surfacing real breakage within the hour.</para>
    /// <para>The readiness gate does <b>not</b> help here and must not be used as one. The readiness
    /// anchors are <c>chatList</c> and <c>rowCell</c>, so a break in those makes the page report "not
    /// ready" as well — suppressing misses while not-ready would mean the single failure that matters most
    /// could never escalate at all.</para>
    /// </remarks>
    public const int BrokenAfterConsecutiveMisses = 10;

    public readonly record struct Entry(
        bool HasManifest,
        bool? Ready,
        string ObservedAgainst,
        int AnchorsResolved,
        IReadOnlyList<string> DegradedAnchors,
        IReadOnlyList<string> BuiltinUsed,
        IReadOnlyList<string> NeverMatched,
        DateTimeOffset AtUtc);

    private static readonly ConcurrentDictionary<string, Entry> Entries =
        new(StringComparer.OrdinalIgnoreCase);

    // instanceId -> anchor -> consecutive scans with no match anywhere.
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> Streaks =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Record(string instanceId, Entry entry)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var id = instanceId.Trim();
        Entries[id] = entry;

        var streak = Streaks.GetOrAdd(id, static _ => new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        foreach (var anchor in entry.NeverMatched)
        {
            streak.AddOrUpdate(anchor, 1, static (_, n) => n + 1);
        }

        // An anchor that matched this time is healthy again; the streak has to reset or a single bad scan
        // during a cold sync would be remembered as breakage forever.
        foreach (var anchor in streak.Keys.Where(k => !entry.NeverMatched.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            streak.TryRemove(anchor, out _);
        }

        LogIfNotable(id, entry, streak);
    }

    public static Entry? TryGet(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && Entries.TryGetValue(instanceId.Trim(), out var entry)
            ? entry
            : null;

    /// <summary>Anchors that have failed to match for at least <see cref="BrokenAfterConsecutiveMisses"/> scans.</summary>
    public static IReadOnlyList<string> BrokenAnchors(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && Streaks.TryGetValue(instanceId.Trim(), out var streak)
            ? streak.Where(kv => kv.Value >= BrokenAfterConsecutiveMisses).Select(kv => kv.Key).Order().ToList()
            : [];

    public static int ConsecutiveMisses(string instanceId, string anchor) =>
        !string.IsNullOrWhiteSpace(instanceId) && Streaks.TryGetValue(instanceId.Trim(), out var streak)
        && streak.TryGetValue(anchor, out var n)
            ? n
            : 0;

    public static int AttemptedCount => Entries.Count;

    /// <summary>True when any account is resolving an anchor below its first candidate.</summary>
    public static bool AnyDegraded =>
        Entries.Values.Any(e => e.DegradedAnchors.Count > 0 || e.BuiltinUsed.Count > 0);

    /// <summary>One line for the Settings health row. Worst state across accounts wins.</summary>
    public static string Describe()
    {
        if (AttemptedCount == 0)
        {
            return "Not yet checked — sync an account to check.";
        }

        var broken = Entries.Keys.SelectMany(BrokenAnchors).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        if (broken.Count > 0)
        {
            var worst = broken[0];
            var scans = Entries.Keys.Max(id => ConsecutiveMisses(id, worst));
            return broken.Count == 1
                ? $"Broken — \"{worst}\" has not been found for {scans} checks in a row. Reading may be incomplete."
                : $"Broken — {broken.Count} page elements have not been found for {scans} checks in a row. Reading may be incomplete.";
        }

        // Not ready, but not yet long enough to be breakage. Saying "healthy" here would be a claim we
        // cannot support, and saying "broken" would be a false alarm on every launch — so say what is
        // actually true, which is that the page has not finished loading its list.
        if (Entries.Values.Any(e => e.Ready == false))
        {
            return "Waiting for WhatsApp to finish loading its chat list.";
        }

        var onBuiltin = Entries.Values.SelectMany(e => e.BuiltinUsed).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (onBuiltin.Count > 0)
        {
            return $"Degraded — {onBuiltin.Count} element{(onBuiltin.Count == 1 ? " is" : "s are")} falling back to the built-in setting. Still reading correctly; the update is out of date.";
        }

        var degraded = Entries.Values.SelectMany(e => e.DegradedAnchors).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (degraded.Count > 0)
        {
            return $"Degraded — {degraded.Count} element{(degraded.Count == 1 ? "" : "s")} matched on a backup setting. Still reading correctly; WhatsApp has changed something.";
        }

        var withManifest = Entries.Values.Count(e => e.HasManifest);
        return withManifest == 0
            ? "No page-element settings loaded — using the versions built into the app."
            : $"Healthy on {withManifest} of {AttemptedCount} account{(AttemptedCount == 1 ? "" : "s")} — every element found on the first try.";
    }

    private static void LogIfNotable(string id, Entry entry, ConcurrentDictionary<string, int> streak)
    {
        var broken = streak.Where(kv => kv.Value >= BrokenAfterConsecutiveMisses).Select(kv => kv.Key).Order().ToList();
        if (broken.Count > 0)
        {
            AppLogger.LogWarningThrottled(
                "Selectors",
                $"[{id}] anchors unmatched for {BrokenAfterConsecutiveMisses}+ scans: {string.Join(", ", broken)}",
                $"selector-broken-{id}");
            return;
        }

        if (entry.BuiltinUsed.Count > 0)
        {
            AppLogger.LogWarningThrottled(
                "Selectors",
                $"[{id}] on built-in fallback for: {string.Join(", ", entry.BuiltinUsed)} — the manifest is behind the client.",
                $"selector-builtin-{id}");
            return;
        }

        if (entry.DegradedAnchors.Count > 0)
        {
            AppLogger.LogWarningThrottled(
                "Selectors",
                $"[{id}] matched below the first candidate for: {string.Join(", ", entry.DegradedAnchors)}.",
                $"selector-degraded-{id}");
        }
    }

    /// <summary>
    /// Parses the page's <c>__umSelectorReport()</c> output. Pure and tolerant: a report this build does
    /// not understand yields null and is skipped, rather than throwing inside a scan.
    /// </summary>
    public static Entry? ParseReport(string? raw, DateTimeOffset atUtc)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var json = raw.Trim();
        try
        {
            // ExecuteScriptAsync returns the JSON *representation* of the value, so a JS string arrives
            // quoted and escaped. Unwrap it before parsing what is inside.
            if (json.StartsWith('"'))
            {
                json = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var degraded = new List<string>();
            var resolved = 0;
            if (root.TryGetProperty("picks", out var picks) && picks.ValueKind == JsonValueKind.Object)
            {
                foreach (var pick in picks.EnumerateObject())
                {
                    resolved++;
                    if (pick.Value.ValueKind == JsonValueKind.Object &&
                        pick.Value.TryGetProperty("index", out var idx) &&
                        idx.ValueKind == JsonValueKind.Number &&
                        idx.TryGetInt32(out var i) &&
                        i > 0)
                    {
                        degraded.Add(pick.Name);
                    }
                }
            }

            return new Entry(
                HasManifest: root.TryGetProperty("hasManifest", out var hm) && hm.ValueKind == JsonValueKind.True,
                Ready: root.TryGetProperty("ready", out var rd)
                    ? rd.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => null
                    }
                    : null,
                ObservedAgainst: root.TryGetProperty("observedAgainst", out var oa) && oa.ValueKind == JsonValueKind.String
                    ? oa.GetString() ?? string.Empty
                    : string.Empty,
                AnchorsResolved: resolved,
                DegradedAnchors: degraded,
                BuiltinUsed: ReadStringArray(root, "builtinUsed"),
                NeverMatched: ReadStringArray(root, "neverMatched"),
                AtUtc: atUtc);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToList();
    }

    /// <summary>The script that produces a report, safe on a page where the adapter has not loaded.</summary>
    public const string ReportScript =
        "(window.__umSelectorReport ? window.__umSelectorReport() : '')";

    internal static void Reset()
    {
        Entries.Clear();
        Streaks.Clear();
    }
}
