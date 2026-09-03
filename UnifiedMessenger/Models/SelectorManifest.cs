using System.Text.Json.Serialization;

namespace UnifiedMessenger.Models;

/// <summary>
/// One named DOM anchor, as ordered fallback candidates rather than a single selector.
/// </summary>
/// <remarks>
/// The ordering is the point. The runtime tries candidates best-first and records <i>which index
/// matched</i>; a rising index is the earliest possible warning that a client redesign is coming, and it
/// arrives before anything breaks. A single selector cannot carry that signal, and neither can the
/// comma-joined "try all" form the scrapers used before the manifest — a comma-joined selector list
/// matches, but never says which member did the matching.
/// </remarks>
public sealed record SelectorAnchor
{
    /// <summary>Selectors to try in order, best-first. Never empty.</summary>
    [JsonPropertyName("candidates")]
    public IReadOnlyList<string> Candidates { get; init; } = [];

    /// <summary>
    /// How to read the anchor's <i>state</i> when the state is not in the markup. <c>"fill"</c> reads the
    /// computed fill colour. Null means the anchor is a plain element lookup.
    /// </summary>
    /// <remarks>
    /// This exists because two measured anchors carry their meaning in colour, not structure: WhatsApp's
    /// delivered-vs-read tick (both are <c>wds-ic-read</c>; only the fill differs) and Google's review
    /// stars (all five are <c>U+E838</c>). Reading the name instead of the colour reports the same value
    /// for every row — which is exactly how five unanswered one-star reviews sat in the queue labelled
    /// "Positive" for the life of that feature. See <c>docs/scraper-inventory/</c>.
    /// </remarks>
    [JsonPropertyName("read")]
    public string? Read { get; init; }

    /// <summary>
    /// When set, only elements whose <c>&lt;title&gt;</c> equals this are the anchor. WhatsApp's
    /// <c>last-msg-status</c> also hosts unrelated icons (a voice-note marker was measured under it), so
    /// without this filter a state read lands on the wrong glyph.
    /// </summary>
    [JsonPropertyName("requireTitle")]
    public string? RequireTitle { get; init; }

    /// <summary>State name to the values that mean it, e.g. <c>read</c> to the blue fill.</summary>
    [JsonPropertyName("states")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? States { get; init; }

    /// <summary>
    /// <c>"first"</c> (default) stops at the first candidate that matches. <c>"union"</c> collects across
    /// every candidate.
    /// </summary>
    /// <remarks>
    /// Union is not a convenience. The unread-badge count sums three different badge markups, and
    /// first-match there would not fail visibly — it would silently undercount unread chats, which is the
    /// single metric this product exists to report.
    /// </remarks>
    [JsonPropertyName("match")]
    public string? Match { get; init; }

    /// <summary>
    /// True when this anchor legitimately may never match on a healthy account, so a miss is not evidence
    /// of breakage and must not escalate to a "broken" warning.
    /// </summary>
    /// <remarks>
    /// Two classes qualify. <b>Conversation-scoped</b> anchors exist only while a chat is open — and under
    /// the read-only rule the app must not open one to find out, so on a healthy install they never match
    /// at all. <b>Dormant fallbacks</b> such as WhatsApp's <c>[data-id]</c>, measured absent from the
    /// entire document and kept only in case it returns. Escalating either would fire a false alarm on
    /// every install, and a false alarm here trains the owner to ignore the one warning that matters.
    /// </remarks>
    [JsonPropertyName("optional")]
    public bool Optional { get; init; }
}

/// <summary>
/// The positive readiness test: which anchors must all resolve before a scan's result may be trusted.
/// </summary>
/// <remarks>
/// Measured need, not caution. WhatsApp Web serves a <b>fully loaded document with an empty chat list</b>
/// during a cold sync — <c>readyState</c> is "complete", every shell anchor is present, and the row count
/// is zero. A scan then reports no conversations, indistinguishably from a genuinely empty account. This
/// is the <c>IsStale</c> vs <c>ReadFailed</c> distinction <see cref="OversightEntityHealth"/> already
/// draws, pushed down to where the read happens.
/// </remarks>
public sealed record SelectorReadiness
{
    [JsonPropertyName("all")]
    public IReadOnlyList<string> All { get; init; } = [];
}

/// <summary>
/// A platform's selector manifest: every DOM anchor its scraper depends on, versioned and loadable at
/// runtime so a client redesign is a data fix rather than a new binary for every customer.
/// </summary>
public sealed record SelectorManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = string.Empty;

    /// <summary>
    /// What this manifest was measured against — client build identifiers and a date, as observed.
    /// </summary>
    /// <remarks>
    /// Deliberately free text rather than a semantic version range. None of these clients expose a version
    /// we can read and compare: WhatsApp Web has only a module-registry size, Messenger only an
    /// <c>rsrc.php</c> hash, Google nothing at all. A structured range here would be a field the loader
    /// could never actually validate, so it records what was seen instead of asserting what is supported.
    /// </remarks>
    [JsonPropertyName("observedAgainst")]
    public string ObservedAgainst { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; init; } = string.Empty;

    [JsonPropertyName("anchors")]
    public IReadOnlyDictionary<string, SelectorAnchor> Anchors { get; init; } =
        new Dictionary<string, SelectorAnchor>();

    [JsonPropertyName("readyWhen")]
    public SelectorReadiness? ReadyWhen { get; init; }
}
