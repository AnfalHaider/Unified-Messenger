using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Every navigation the product performs, declared in one place: what it targets, what proves it arrived,
/// what it costs the customer, and how long it is allowed to keep trying.
/// </summary>
/// <remarks>
/// Registered rather than scattered because three separate rules only hold if there is somewhere to state
/// them: arrival must be proven independently of the navigator, an operation with a customer-visible side
/// effect must never run from a background scan, and a retry budget must be a stated bound rather than a
/// loop someone tuned once. Adding a channel means adding rows here, and the tests below then hold the new
/// rows to the same bar as the old ones.
/// </remarks>
public static class NavigationOperations
{
    public const string FocusConversation = "focus-conversation";
    public const string ShowArchived = "show-archived";
    public const string OpenReviewsManager = "open-reviews-manager";
    public const string OpenMerchantView = "open-merchant-view";

    public static IReadOnlyList<NavigationOperation> All { get; } =
    [
        new NavigationOperation
        {
            Id = FocusConversation,
            PlatformId = "whatsapp",
            Kind = NavigationKind.Dom,
            Description = "Scroll the chat list to one conversation and open it.",
            // The composer is the load-bearing one: it exists ONLY while a chat is open, so its presence
            // separates "my selector is stale" from "the click did nothing" — which a header alone cannot.
            ReadbackAnchors = ["openChatPane", "composer"],
            // Opening a chat marks it read. On WhatsApp that is the owner's own account and the receipt
            // goes to a customer they were about to reply to anyway — but it is still a side effect, and
            // the flag is what stops a future background scan calling this because it happened to be handy.
            RequiresUserIntent = true,
            MaxAttempts = 16,
            RetryDelayMs = 700,
            Notes = "WhatsApp Web has no per-conversation URL, so this is DOM-driven of necessity. "
                + "Whether web.whatsapp.com/send?phone= could replace it is the open question in "
                + "docs/scraper-inventory/whatsapp.md — untested, because ?text= is documented to prefill a "
                + "draft and it needs a number the owner controls."
        },
        new NavigationOperation
        {
            Id = ShowArchived,
            PlatformId = "whatsapp",
            Kind = NavigationKind.Dom,
            Description = "Open the archived conversations panel.",
            // Measured live 2026-09-02: the archived panel is its own container with its own testid, and
            // #pane-side keeps the main list underneath (the row count does not change). So the readback
            // must be the panel's own anchor — a row count or a Back button would both be misread.
            ReadbackAnchors = ["archivedPanel"],
            RequiresUserIntent = false,
            MaxAttempts = 6,
            RetryDelayMs = 500,
            Notes = "Read-only: it opens a list, not a conversation, so nothing is marked read."
        },
        new NavigationOperation
        {
            Id = OpenReviewsManager,
            PlatformId = "googlebusiness",
            Kind = NavigationKind.Url,
            Description = "Return to the Google reviews manager from wherever the WebView is parked.",
            ReadbackAnchors = [],
            RequiresUserIntent = false,
            ImplementedInPage = true,
            Notes = "business.google.com/reviews. Implemented inside GoogleReviewSnapshotService's own "
                + "kickoff script, which already proves arrival through its state machine rather than a "
                + "DOM anchor. Registered so the inventory is complete, and pinned by a test: the guard "
                + "must accept ANY google.com host, because the rating scrape parks this same WebView on "
                + "the Search merchant view and a business.google.com-only test strands it there."
        },
        new NavigationOperation
        {
            Id = OpenMerchantView,
            PlatformId = "googlebusiness",
            Kind = NavigationKind.Url,
            Description = "Reach the Search merchant view, the only place the rating and lifetime total exist.",
            ReadbackAnchors = [],
            RequiresUserIntent = false,
            ImplementedInPage = true,
            Notes = "Navigates to business.google.com/ and follows Google's own redirect. Costs a visible "
                + "round trip, so it is throttled and must not run on the account currently on screen "
                + "unless the owner asked."
        }
    ];

    public static NavigationOperation? Find(string id) =>
        All.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    public static NavigationOperation Require(string id) =>
        Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "No such navigation operation.");

    /// <summary>
    /// Whether an operation may run right now. An operation that changes something the customer can see
    /// needs the caller to state that a person asked for it.
    /// </summary>
    public static bool MayRun(NavigationOperation operation, bool userInitiated)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!operation.RequiresUserIntent || userInitiated)
        {
            return true;
        }

        AppLogger.LogWarning(
            "Navigate",
            $"Refused '{operation.Id}': it changes something the customer can see and was not asked for by the person.");
        return false;
    }

    /// <summary>
    /// The script that answers "am I there?" for an operation, built from its declared readback anchors.
    /// Returns <c>"true"</c>/<c>"false"</c>, or an empty string when the operation proves arrival some
    /// other way.
    /// </summary>
    /// <remarks>
    /// Reads through <c>__umPick</c> so the anchors come from the selector manifest, with the built-ins
    /// below as the fallback for a page whose adapter has not loaded — the same contract as everywhere else.
    /// </remarks>
    public static string BuildReadbackScript(NavigationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.ReadbackAnchors.Count == 0)
        {
            return string.Empty;
        }

        var checks = operation.ReadbackAnchors
            .Select(a => $"(window.__umPick?window.__umPick('{a}',{JsFallbackFor(a)}).length>0:!!document.querySelector({JsFallbackFor(a)}))");

        return "(function(){try{return " + string.Join("&&", checks) + ";}catch(e){return false;}})()";
    }

    // The selector each readback anchor falls back to when no manifest is loaded. Deliberately duplicated
    // from the manifest rather than read out of it: this is the "the manifest failed" path, so it cannot
    // depend on the manifest.
    private static string JsFallbackFor(string anchor) => anchor switch
    {
        "openChatPane" => "'#main'",
        // Deliberately the BROAD form, matching what ConversationFocusHelper's readback script has always
        // used. A narrower `#main [contenteditable][role="textbox"]` would mean a WhatsApp build whose
        // composer lacks role="textbox", or puts it in the footer, fails this readback — and because the
        // readback now GATES focus, that would report failure on a conversation that opened perfectly well,
        // sixteen times per click. When the readback decides, narrowing it is not a tidy-up.
        "composer" => "'#main [contenteditable=\"true\"],footer [contenteditable=\"true\"]'",
        "archivedPanel" => "'[data-testid=\"archived-chatlist\"]'",
        _ => "'#pane-side'"
    };
}
