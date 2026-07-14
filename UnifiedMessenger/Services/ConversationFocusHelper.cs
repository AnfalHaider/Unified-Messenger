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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(conversationKey))
        {
            return false;
        }

        var script = WebViewScriptBuilder.BuildIifeFunctionCall(
            "__umFocusConversation",
            [instance.Platform, conversationKey.Trim(), customerName ?? string.Empty]);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, script)
                .ConfigureAwait(false);

            if (ParseScriptBoolean(raw))
            {
                return true;
            }

            if (attempt < MaxAttempts - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }
}
