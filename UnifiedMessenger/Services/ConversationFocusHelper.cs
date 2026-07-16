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
                await DrainTraceAsync(sessionManager, instance, attempt, true).ConfigureAwait(false);
                return true;
            }

            if (attempt < MaxAttempts - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        await DrainTraceAsync(sessionManager, instance, MaxAttempts, false).ConfigureAwait(false);
        return false;
    }

    // Writes the page-side breadcrumb trail to app.log. Focus reporting "true" is NOT the same as the right
    // chat opening — an unverified top-result click also returns true — so this logs on success too, and the
    // trace is what tells the two apart.
    private static async Task DrainTraceAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance,
        int attempts,
        bool focused)
    {
        try
        {
            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, "JSON.stringify(window.__umFocusTrace||[])")
                .ConfigureAwait(false);

            var trace = string.IsNullOrWhiteSpace(raw) ? "<none>" : raw.Trim();
            AppLogger.LogInfo(
                "focus",
                $"{instance.DisplayName}: focused={focused} attempts={attempts} trace={trace}");
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("focus", $"trace drain failed: {ex.Message}");
        }
    }
}
