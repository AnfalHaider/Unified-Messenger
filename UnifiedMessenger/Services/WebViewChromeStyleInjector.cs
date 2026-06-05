using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Injects per-platform chrome CSS into WebView2 documents so messenger sites blend with the app shell (ADAPT-06).
/// Base seam styles live in <c>generic-chrome.css</c>; platform files add selectors only.
/// </summary>
public static class WebViewChromeStyleInjector
{
    private const string GenericFileName = "generic-chrome.css";
    private const string StyleElementId = "unified-messenger-chrome";

    private static readonly string StylesRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Styles");
    private static readonly ConditionalWeakTable<CoreWebView2, object> RegisteredWebViews = new();
    private static readonly Dictionary<string, string> CssCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();

    public static async Task InjectAsync(
        CoreWebView2 coreWebView,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);

        var css = await LoadChromeCssAsync(platformId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(css))
        {
            return;
        }

        await UiThreadRunner.RunAsync(async () =>
        {
            if (!TryMarkRegistered(coreWebView))
            {
                return;
            }

            await coreWebView
                .AddScriptToExecuteOnDocumentCreatedAsync(BuildDocumentCreatedScript(css))
                .AsTask()
                .ConfigureAwait(true);
        }).ConfigureAwait(true);

        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
    }

    internal static string ResolvePlatformStylesheetFileName(string? platformId)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);
        if (normalized.Equals("generic", StringComparison.OrdinalIgnoreCase))
        {
            return GenericFileName;
        }

        var platformFile = $"{normalized}-chrome.css";
        return File.Exists(Path.Combine(StylesRoot, platformFile)) ? platformFile : GenericFileName;
    }

    internal static async Task<string> LoadChromeCssAsync(
        string? platformId,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);

        lock (CacheLock)
        {
            if (CssCache.TryGetValue(normalized, out var cached))
            {
                return cached;
            }
        }

        var css = await LoadChromeCssCoreAsync(normalized, cancellationToken).ConfigureAwait(false);

        lock (CacheLock)
        {
            CssCache[normalized] = css;
        }

        return css;
    }

    internal static string BuildDocumentCreatedScript(string css)
    {
        var cssLiteral = JsonSerializer.Serialize(css);
        return $$"""
            (function () {
              if (document.getElementById('{{StyleElementId}}')) { return; }
              var style = document.createElement('style');
              style.id = '{{StyleElementId}}';
              style.textContent = {{cssLiteral}};
              (document.head || document.documentElement).appendChild(style);
            })();
            """;
    }

    private static bool TryMarkRegistered(CoreWebView2 coreWebView)
    {
        if (RegisteredWebViews.TryGetValue(coreWebView, out _))
        {
            return false;
        }

        RegisteredWebViews.Add(coreWebView, null!);
        return true;
    }

    private static async Task<string> LoadChromeCssCoreAsync(
        string normalizedPlatformId,
        CancellationToken cancellationToken)
    {
        var genericPath = Path.Combine(StylesRoot, GenericFileName);
        var genericCss = File.Exists(genericPath)
            ? await File.ReadAllTextAsync(genericPath, cancellationToken).ConfigureAwait(false)
            : string.Empty;

        if (normalizedPlatformId.Equals("generic", StringComparison.OrdinalIgnoreCase))
        {
            return genericCss;
        }

        var platformPath = Path.Combine(StylesRoot, $"{normalizedPlatformId}-chrome.css");
        if (!File.Exists(platformPath))
        {
            return genericCss;
        }

        var platformCss = await File.ReadAllTextAsync(platformPath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(platformCss))
        {
            return genericCss;
        }

        return string.IsNullOrWhiteSpace(genericCss)
            ? platformCss
            : genericCss + Environment.NewLine + platformCss;
    }
}
