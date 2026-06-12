using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class ConversationFocusHelper
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

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

        var script = WebViewScriptBuilder.BuildFunctionCall(
            "__umFocusConversation",
            [instance.Platform, conversationKey.Trim(), customerName ?? string.Empty]);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, script)
                .ConfigureAwait(false);

            if (ParseScriptBoolean(raw))
            {
                return true;
            }

            if (attempt < 2)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }
}
