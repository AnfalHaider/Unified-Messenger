using System.Text.Json;
using Microsoft.Web.WebView2.Core;

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

        var webView = InstanceWebViewRegistry.Instance.TryGet(instanceId);
        var coreWebView = webView?.CoreWebView2;
        if (coreWebView is null)
        {
            return false;
        }

        var trimmed = draftText.Length <= MaxDraftLength
            ? draftText.Trim()
            : draftText[..MaxDraftLength].Trim();

        var payload = JsonSerializer.Serialize(trimmed);
        var script = $"window.__umInjectDraftReply({payload});";

        try
        {
            var result = await UiThreadRunner.RunAsync(async () =>
                    await coreWebView.ExecuteScriptAsync(script))
                .ConfigureAwait(false);

            return ParseInjectResult(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Draft injection failed for {instanceId}: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> ClearDraftAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        var webView = InstanceWebViewRegistry.Instance.TryGet(instanceId);
        var coreWebView = webView?.CoreWebView2;
        if (coreWebView is null)
        {
            return false;
        }

        try
        {
            var result = await UiThreadRunner.RunAsync(async () =>
                    await coreWebView.ExecuteScriptAsync("window.__umClearDraftReply();"))
                .ConfigureAwait(false);

            return ParseInjectResult(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Draft clear failed for {instanceId}: {ex.Message}");
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
