using System.Text;
using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

public sealed class HotkeyCopilotOrchestrator
{
    private static readonly Lazy<HotkeyCopilotOrchestrator> LazyInstance =
        new(() => new HotkeyCopilotOrchestrator());

    private readonly SemaphoreSlim _runGate = new(1, 1);

    public static HotkeyCopilotOrchestrator Instance => LazyInstance.Value;

    public async Task TryRunCopilotAsync(CancellationToken cancellationToken = default)
    {
        if (!AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            return;
        }

        if (!await _runGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var instanceId = ResolveTargetInstanceId();
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            var context = await ConversationContextScraper
                .ExtractAsync(instanceId, maxMessages: 4, cancellationToken)
                .ConfigureAwait(false);

            if (context is null || !context.Ok)
            {
                return;
            }

            var platform = string.IsNullOrWhiteSpace(context.Platform) ? "generic" : context.Platform;
            var customerName = context.Messages.LastOrDefault(m => m.Direction == "incoming")?.Text;
            var lastInbound = string.IsNullOrWhiteSpace(context.LastIncomingMessage)
                ? context.Messages.LastOrDefault()?.Text ?? string.Empty
                : context.LastIncomingMessage;

            if (lastInbound.Trim().Length < 2)
            {
                return;
            }

            var prompt = AiDraftPromptService.BuildPrompt(
                platform,
                lastInbound,
                customerName,
                BuildTranscriptHint(context.Messages));

            await WebViewDraftInjector.ResetDraftStreamAsync(instanceId, cancellationToken)
                .ConfigureAwait(false);

            var pending = new StringBuilder();
            var lastFlush = Environment.TickCount64;
            const int flushIntervalMs = 60;

            await foreach (var token in OllamaOrchestrationService.Instance
                               .StreamGenerateAsync(
                                   prompt.UserPrompt,
                                   prompt.SystemPrompt,
                                   priority: InferencePriority.Interactive,
                                   cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
            {
                pending.Append(token);
                var now = Environment.TickCount64;
                if (now - lastFlush < flushIntervalMs)
                {
                    continue;
                }

                await FlushPendingAsync(instanceId, pending, cancellationToken).ConfigureAwait(false);
                lastFlush = now;
            }

            await FlushPendingAsync(instanceId, pending, cancellationToken).ConfigureAwait(false);
            await WebViewDraftInjector.FinalizeDraftStreamAsync(instanceId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // user or preempted background work
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hotkey copilot failed: {ex.Message}");
        }
        finally
        {
            _runGate.Release();
        }
    }

    private static async Task FlushPendingAsync(
        string instanceId,
        StringBuilder pending,
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

    private static string? ResolveTargetInstanceId()
    {
        var active = ActiveWorkspaceContext.CurrentInstanceId;
        if (!string.IsNullOrWhiteSpace(active))
        {
            return active;
        }

        return InstanceSessionManager.Instance.VisibleInstanceId;
    }

    private static string BuildTranscriptHint(IReadOnlyList<ConversationMessageEntry> messages)
    {
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        var lines = messages
            .TakeLast(4)
            .Select(m => $"{m.Direction}: {m.Text}");
        return "Recent transcript:\n" + string.Join("\n", lines);
    }
}

public sealed class ConversationContextResult
{
    public bool Ok { get; init; }

    public string Platform { get; init; } = "generic";

    public IReadOnlyList<ConversationMessageEntry> Messages { get; init; } = [];

    public string LastIncomingMessage { get; init; } = string.Empty;
}

public sealed class ConversationMessageEntry
{
    public string Text { get; init; } = string.Empty;

    public string Direction { get; init; } = "incoming";
}

public static class ConversationContextScraper
{
    public static async Task<ConversationContextResult?> ExtractAsync(
        string instanceId,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var webView = InstanceWebViewRegistry.Instance.TryGet(instanceId);
        var coreWebView = webView?.CoreWebView2;
        if (coreWebView is null)
        {
            return null;
        }

        var script = $"window.__umExtractConversationContext({maxMessages});";

        try
        {
            var raw = await UiThreadRunner.RunAsync(async () =>
                    await coreWebView.ExecuteScriptAsync(script))
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var ok = root.TryGetProperty("ok", out var okElement) &&
                     okElement.ValueKind == JsonValueKind.True;

            var messages = new List<ConversationMessageEntry>();
            if (root.TryGetProperty("messages", out var messagesElement) &&
                messagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in messagesElement.EnumerateArray())
                {
                    var text = entry.TryGetProperty("text", out var textElement)
                        ? textElement.GetString() ?? string.Empty
                        : string.Empty;
                    var direction = entry.TryGetProperty("direction", out var directionElement)
                        ? directionElement.GetString() ?? "incoming"
                        : "incoming";

                    if (text.Length > 0)
                    {
                        messages.Add(new ConversationMessageEntry
                        {
                            Text = text,
                            Direction = direction
                        });
                    }
                }
            }

            var lastIncoming = root.TryGetProperty("lastIncomingMessage", out var lastElement)
                ? lastElement.GetString() ?? string.Empty
                : string.Empty;

            var platform = root.TryGetProperty("platform", out var platformElement)
                ? platformElement.GetString() ?? "generic"
                : "generic";

            return new ConversationContextResult
            {
                Ok = ok,
                Platform = platform,
                Messages = messages,
                LastIncomingMessage = lastIncoming
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Conversation context scrape failed: {ex.Message}");
            return null;
        }
    }
}
