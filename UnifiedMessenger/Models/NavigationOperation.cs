namespace UnifiedMessenger.Models;

/// <summary>How a navigation reaches its destination.</summary>
public enum NavigationKind
{
    /// <summary>A real URL. Preferred wherever the client has one — nothing to click, nothing to match.</summary>
    Url,

    /// <summary>Driven by clicking the page, because the client has no address for this view.</summary>
    Dom
}

/// <summary>
/// One named, testable navigation the product performs: what it targets, and — the part that matters —
/// what independently proves it arrived.
/// </summary>
/// <remarks>
/// <para><b>Why arrival needs its own proof.</b> Clicking is not opening. The WhatsApp focus path returns
/// true whether or not the chat opened: it finds a matching row, calls <c>.click()</c>, and reports
/// success. If WhatsApp's row handler ever wants <c>pointerdown</c> instead, that click does nothing and
/// still reports a clean success. So every operation here names an anchor whose presence means the view
/// is genuinely on screen, and the runner believes that rather than the navigator's own return value.</para>
/// <para><b>Why the side-effect flag is declared, not inferred.</b> Opening a conversation marks it read,
/// and on Meta that fires a receipt the customer sees which cannot be withdrawn. Such an operation is not
/// read-only and must never run from a background scan — only from something the person actually did. The
/// flag is set on the operation so that constraint exists before any adapter can violate it, the same way
/// <see cref="PlatformCapabilities.RequiresThreadOpenToRead"/> was declared before Meta had a scraper.</para>
/// </remarks>
public sealed record NavigationOperation
{
    /// <summary>Stable id, used in logs and by callers. Kebab-case.</summary>
    public required string Id { get; init; }

    /// <summary>Platform this operation belongs to, or empty when it applies to a family.</summary>
    public required string PlatformId { get; init; }

    public required NavigationKind Kind { get; init; }

    /// <summary>What the operation is for, in the words a person would use.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Manifest anchor names that must all resolve for the operation to count as arrived. Empty only for
    /// an operation whose arrival is proven some other way, which must then be said in <see cref="Notes"/>.
    /// </summary>
    public IReadOnlyList<string> ReadbackAnchors { get; init; } = [];

    /// <summary>
    /// True when performing this changes something the customer can see — a read receipt, a "Seen" mark.
    /// The runner refuses to perform it unless the caller states the person asked for it.
    /// </summary>
    public bool RequiresUserIntent { get; init; }

    /// <summary>How many attempts, and how long between them. Stated, not improvised at the call site.</summary>
    public int MaxAttempts { get; init; } = 1;

    public int RetryDelayMs { get; init; } = 700;

    /// <summary>
    /// True when the shipped implementation lives inside the page's own script rather than being driven by
    /// the runner. Registered anyway so the inventory of navigations is complete and testable.
    /// </summary>
    public bool ImplementedInPage { get; init; }

    public string Notes { get; init; } = string.Empty;

    /// <summary>The whole retry budget, for logging a bound rather than a vague "it kept trying".</summary>
    public TimeSpan Budget => TimeSpan.FromMilliseconds((long)MaxAttempts * RetryDelayMs);
}

/// <summary>
/// What a navigation actually achieved, separating the three questions that used to collapse into one bool.
/// </summary>
/// <param name="Arrived">The independent readback says the destination is on screen.</param>
/// <param name="IdentityVerified">
/// What arrived is what was asked for. Distinct from <paramref name="Arrived"/> on purpose: row matching
/// is a substring test across every rendered row, so a wrong-but-plausible match reads exactly like a
/// correct one unless the two are compared.
/// </param>
/// <param name="Reached">What was actually reached, for the trace. Never logged with customer text.</param>
public readonly record struct NavigationOutcome(
    bool Arrived,
    bool IdentityVerified,
    string Reached,
    int Attempts,
    string OperationId)
{
    public static NavigationOutcome Refused(string operationId) =>
        new(false, false, "refused", 0, operationId);
}
