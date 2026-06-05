namespace UnifiedMessenger.Services;

/// <summary>
/// Batches streamed LLM tokens before posting them into the WebView compose field.
/// </summary>
internal static class DraftStreamFlushHelper
{
    internal const int DefaultFlushIntervalMs = 60;

    internal static async Task FlushPendingAsync(
        string instanceId,
        System.Text.StringBuilder pending,
        CancellationToken cancellationToken)
    {
        if (pending.Length == 0)
        {
            return;
        }

        var chunk = pending.ToString();
        pending.Clear();
        await WebViewDraftInjector.AppendDraftChunkAsync(instanceId, chunk, cancellationToken)
            .ConfigureAwait(false);
    }
}
