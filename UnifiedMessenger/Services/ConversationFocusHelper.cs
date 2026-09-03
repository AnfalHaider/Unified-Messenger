using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class ConversationFocusHelper
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(700);

    // ~11s of attempts: a cold/just-switched WhatsApp webview needs several seconds to restore its session
    // and render the chat list before __umFocusConversation can find (or search for) the target. The old
    // 2.5s window expired first — the account opened, showed "loading", then gave up ("nothing"). Success
    // returns on the first ready attempt, so warm accounts still focus instantly.
    private const int MaxAttempts = 16;

    /// <summary>Let the clicked chat render before reading back which one is open.</summary>
    private static readonly TimeSpan OpenChatSettleDelay = TimeSpan.FromMilliseconds(900);

    // Reads back the conversation actually on screen. Several header selectors because WhatsApp's markup
    // shifts between builds, and it also reports the composer box — which only exists while a chat is open, so
    // "no chat opened" stays distinguishable from "my header selector is wrong". Without that second signal a
    // stale selector would look exactly like the no-op click it is meant to detect.
    // The header list, the open-chat pane and the composer all come from the selector manifest when one is
    // loaded (window.__umCandidates / __umPick1, defined by adapter-core.js), and fall back to the literals
    // below when it is not. The fallbacks are not decoration: this script also runs immediately after a
    // WebView reload, before the adapter chain has re-injected, and a readback that threw there would report
    // "chat did not open" for a chat that opened perfectly well.
    private const string OpenChatHeaderScript =
        "(function(){try{" +
        "var dsels=['#main header span[title]','#main header [data-testid=\"conversation-info-header\"] span'," +
        "'#main header span[dir=\"auto\"]','[data-testid=\"conversation-header\"] span[title]'];" +
        "var sels=window.__umCandidates?window.__umCandidates('conversationHeaderReadback',dsels):dsels;" +
        "var hdr='',hit=-1;" +
        "for(var i=0;i<sels.length;i++){var e=document.querySelector(sels[i]);" +
        "if(e){var t=(e.getAttribute('title')||e.textContent||'').trim();if(t){hdr=t;hit=i;break;}}}" +
        "var main=window.__umPick1?!!window.__umPick1('openChatPane','#main'):!!document.querySelector('#main');" +
        "var csel='#main [contenteditable=\"true\"],footer [contenteditable=\"true\"]';" +
        "var composer=window.__umPick1?!!window.__umPick1('composer',csel):!!document.querySelector(csel);" +
        "return JSON.stringify({header:hdr,sel:hit,main:main,composer:composer});" +
        "}catch(e){return '<err>';}})()";

    public static bool ParseScriptBoolean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim().Trim('"');
        return trimmed.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> TryFocusConversationWithRetryAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance,
        string? conversationKey,
        string? customerName,
        CancellationToken cancellationToken = default,
        string? contactPhone = null)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(conversationKey))
        {
            return false;
        }

        var script = WebViewScriptBuilder.BuildIifeFunctionCall(
            "__umFocusConversation",
            [instance.Platform, conversationKey.Trim(), customerName ?? string.Empty, contactPhone ?? string.Empty]);

        var want = $"name='{customerName}' phone='{contactPhone}' key='{conversationKey.Trim()}'";

        // Start each focus session with a clean trail so the drained log covers this click only.
        await sessionManager
            .TryExecuteScriptOnInstanceAsync(instance.Id, "window.__umFocusTrace=[];")
            .ConfigureAwait(false);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, script)
                .ConfigureAwait(false);

            if (ParseScriptBoolean(raw))
            {
                // The click reported success — now find out whether anything actually opened. This used to
                // return here, trusting the navigator's own answer, with the readback happening afterwards
                // purely to write a log line nobody was reading. But the click paths report true whether or
                // not the chat opened: they find a matching row, call .click(), and say yes. If WhatsApp's
                // row handler ever wants pointerdown instead, that click does nothing and still reports a
                // clean success — and the caller scrolls the owner to a conversation that is not on screen.
                await Task.Delay(OpenChatSettleDelay, cancellationToken).ConfigureAwait(false);
                var arrived = await ReadArrivedAsync(sessionManager, instance).ConfigureAwait(false);

                // Null means the readback itself could not be evaluated (page mid-navigation, script error).
                // That is not evidence of failure, so fall back to the old behaviour rather than inventing
                // one — a readback that cannot run must not be able to fail an operation that worked.
                if (arrived != false)
                {
                    await DrainTraceAsync(sessionManager, instance, want, attempt, true, arrived).ConfigureAwait(false);
                    return true;
                }

                // Clicked, claimed success, nothing opened. Keep trying within the stated budget.
                AppLogger.LogWarning(
                    "Navigate",
                    $"{instance.DisplayName}: focus reported success but no conversation is open (attempt {attempt + 1}/{MaxAttempts}).");
            }

            if (attempt < MaxAttempts - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        await DrainTraceAsync(sessionManager, instance, want, MaxAttempts, false, null).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// The independent proof that a conversation is on screen, from the declared readback anchors of the
    /// <c>focus-conversation</c> operation. Null when the readback could not be evaluated at all.
    /// </summary>
    private static async Task<bool?> ReadArrivedAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance)
    {
        try
        {
            var script = NavigationOperations.BuildReadbackScript(
                NavigationOperations.Require(NavigationOperations.FocusConversation));

            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, script)
                .ConfigureAwait(false);

            var text = raw?.Trim().Trim('"');
            return text switch
            {
                "true" => true,
                "false" => false,
                _ => null
            };
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Navigate", $"Focus readback failed for {instance.Id}: {ex.Message}");
            return null;
        }
    }

    // Writes the page-side breadcrumb trail to app.log. Two things make the obvious signals untrustworthy, so
    // both halves have to be logged together:
    //   • focus returning "true" is NOT the same as the right chat opening (an unverified top-result click
    //     also returns true), and
    //   • the trace records the title it CLICKED, which only means anything next to the target it WANTED —
    //     row matching is a substring test across every rendered row, so a wrong-but-plausible match reads
    //     identically to a correct one unless you can compare the two.
    private static async Task DrainTraceAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance,
        string want,
        int attempts,
        bool focused,
        bool? arrived)
    {
        try
        {
            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, "JSON.stringify(window.__umFocusTrace||[])")
                .ConfigureAwait(false);

            // The header is read for the TRACE — to compare what was reached against what was wanted, since
            // a wrong-but-plausible row match reads identically to a correct one. Whether anything opened at
            // all is decided before this, by ReadArrivedAsync, and that decision gates the return value.
            var header = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, OpenChatHeaderScript)
                .ConfigureAwait(false);

            var trace = string.IsNullOrWhiteSpace(raw) ? "<none>" : raw.Trim();
            var opened = string.IsNullOrWhiteSpace(header) ? "<null>" : header.Trim();
            var budget = NavigationOperations.Require(NavigationOperations.FocusConversation).Budget;
            var arrivedText = arrived switch { true => "yes", false => "no", _ => "unknown" };
            AppLogger.LogInfo(
                "focus",
                $"{instance.DisplayName}: want={want} focused={focused} arrived={arrivedText} "
                + $"attempts={attempts}/{MaxAttempts} budget={budget.TotalSeconds:0.#}s opened={opened} trace={trace}");
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("focus", $"trace drain failed: {ex.Message}");
        }
    }
}
