using System.Text.Json;

namespace UnifiedMessenger.Services;

public static class WebViewDraftInjector
{
    public const int MaxDraftLength = 6000;

    public static async Task<bool> InjectDraftAsync(
        string instanceId,
        string draftText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(draftText))
        {
            return false;
        }

        var trimmed = draftText.Length <= MaxDraftLength
            ? draftText.Trim()
            : draftText[..MaxDraftLength].Trim();

        return await ExecuteInjectScriptAsync(
                instanceId,
                $"window.__umInjectDraftReply({JsonSerializer.Serialize(trimmed)});",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> ClearDraftAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        return await ExecuteInjectScriptAsync(
                instanceId,
                "window.__umClearDraftReply();",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> ResetDraftStreamAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        return await ExecuteInjectScriptAsync(
                instanceId,
                "window.__umResetDraftStream();",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> AppendDraftChunkAsync(
        string instanceId,
        string chunk,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(chunk))
        {
            return false;
        }

        return await ExecuteInjectScriptAsync(
                instanceId,
                $"window.__umAppendDraftChunk({JsonSerializer.Serialize(chunk)});",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> FinalizeDraftStreamAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        return await ExecuteInjectScriptAsync(
                instanceId,
                "window.__umFinalizeDraftStream();",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<bool> ExecuteInjectScriptAsync(
        string instanceId,
        string script,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await WebViewScriptGateway.Instance
                .ExecutePreparedScriptAsync(instanceId, script, cancellationToken)
                .ConfigureAwait(false);

            return ParseInjectResult(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Draft injection failed for {instanceId}: {ex.Message}");
            return false;
        }
    }

    internal static bool ParseInjectResult(string? rawResult)
    {
        if (string.IsNullOrWhiteSpace(rawResult))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawResult);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (root.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (root.TryGetProperty("ok", out var okElement))
            {
                return okElement.ValueKind == JsonValueKind.True;
            }
        }
        catch (JsonException)
        {
            return rawResult.Contains("true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
