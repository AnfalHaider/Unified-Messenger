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

    // Reads the conversation currently open in the main pane. Several selectors because WhatsApp's header
    // markup shifts between builds — reports which one hit so a "<none>" is distinguishable from "wrong chat".
    private const string OpenChatHeaderScript =
        "(function(){try{" +
        "var sels=['#main header span[title]','#main header [data-testid=\"conversation-info-header\"] span'," +
        "'#main header span[dir=\"auto\"]','[data-testid=\"conversation-header\"] span[title]'];" +
        "for(var i=0;i<sels.length;i++){var e=document.querySelector(sels[i]);" +
        "if(e){var t=(e.getAttribute('title')||e.textContent||'').trim();if(t)return '['+i+'] '+t;}}" +
        "return document.querySelector('#main')?'<main-but-no-header>':'<no-main-pane>';" +
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
                await DrainTraceAsync(sessionManager, instance, want, attempt, true).ConfigureAwait(false);
                return true;
            }

            if (attempt < MaxAttempts - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        await DrainTraceAsync(sessionManager, instance, want, MaxAttempts, false).ConfigureAwait(false);
        return false;
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
        bool focused)
    {
        try
        {
            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, "JSON.stringify(window.__umFocusTrace||[])")
                .ConfigureAwait(false);

            // Nothing in the click paths verifies that clicking a row actually OPENED the chat — they click
            // and report true. If WhatsApp's row handler wants pointerdown/mousedown rather than click, then
            // .click() does nothing and still logs a clean success. So read back the conversation actually on
            // screen: that is the only thing that distinguishes "focused" from "claims to have focused".
            await Task.Delay(OpenChatSettleDelay).ConfigureAwait(false);
            var header = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, OpenChatHeaderScript)
                .ConfigureAwait(false);

            var trace = string.IsNullOrWhiteSpace(raw) ? "<none>" : raw.Trim();
            var opened = string.IsNullOrWhiteSpace(header) ? "<null>" : header.Trim();
            AppLogger.LogInfo(
                "focus",
                $"{instance.DisplayName}: want={want} focused={focused} attempts={attempts} opened={opened} trace={trace}");
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("focus", $"trace drain failed: {ex.Message}");
        }
    }
}
