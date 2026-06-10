using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Waits for a WebView session to accept conversation-focus scripts before giving up.
/// </summary>
public static class ConversationFocusHelper
{
    public const int DefaultTimeoutMs = 2000;

    public const int InitialDelayMs = 150;

    public const int MaxDelayMs = 400;

    public static async Task<bool> TryFocusConversationWithRetryAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance,
        string? conversationKey,
        string? customerName,
        int timeoutMs = DefaultTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(instance);

        var script = WebViewScriptBuilder.BuildFunctionCall(
            "__umFocusConversation",
            [
                PlatformDefinition.NormalizePlatformId(instance.Platform),
                conversationKey ?? string.Empty,
                customerName ?? string.Empty
            ]);

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var delayMs = InitialDelayMs;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var rawResult = await sessionManager
                    .TryExecuteScriptOnInstanceAsync(instance.Id, script)
                    .ConfigureAwait(false);

                if (ParseScriptBoolean(rawResult))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Conversation focus attempt failed: {ex.Message}");
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var wait = TimeSpan.FromMilliseconds(Math.Min(delayMs, remaining.TotalMilliseconds));
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            delayMs = Math.Min(delayMs * 2, MaxDelayMs);
        }

        return false;
    }

    internal static bool ParseScriptBoolean(string? scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult))
        {
            return false;
        }

        var normalized = scriptResult.Trim().Trim('"');
        return normalized.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
