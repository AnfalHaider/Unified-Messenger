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
                .ExtractAsync(instanceId, maxMessages: 8, cancellationToken)
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

            var prompt = await BuildCopilotPromptAsync(
                    instanceId,
                    platform,
                    customerName,
                    lastInbound,
                    context.Messages,
                    cancellationToken)
                .ConfigureAwait(false);

            await WebViewDraftInjector.ResetDraftStreamAsync(instanceId, cancellationToken)
                .ConfigureAwait(false);

            var pending = new StringBuilder();
            var lastFlush = Environment.TickCount64;

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
                if (now - lastFlush < DraftStreamFlushHelper.DefaultFlushIntervalMs)
                {
                    continue;
                }

                await DraftStreamFlushHelper
                    .FlushPendingAsync(instanceId, pending, cancellationToken)
                    .ConfigureAwait(false);
                lastFlush = now;
            }

            await DraftStreamFlushHelper
                .FlushPendingAsync(instanceId, pending, cancellationToken)
                .ConfigureAwait(false);
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

    private static string? ResolveTargetInstanceId()
    {
        var active = ActiveWorkspaceContext.CurrentInstanceId;
        if (!string.IsNullOrWhiteSpace(active))
        {
            return active;
        }

        return InstanceSessionManager.Instance.VisibleInstanceId;
    }

    private static async Task<AiDraftPromptRequest> BuildCopilotPromptAsync(
        string instanceId,
        string platform,
        string? customerName,
        string lastInbound,
        IReadOnlyList<ConversationMessageEntry> messages,
        CancellationToken cancellationToken)
    {
        if (!WhatsAppOperationalContextBuilder.IsWhatsAppPlatform(platform))
        {
            return AiDraftPromptService.BuildPrompt(
                platform,
                lastInbound,
                customerName,
                BuildTranscriptHint(messages));
        }

        var instance = await TryLoadInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        var conversationKey = ResolveConversationKey(instanceId);
        var whatsAppContext = string.IsNullOrWhiteSpace(conversationKey)
            ? null
            : WhatsAppBusinessContextService.Instance.GetThreadContext(instanceId, conversationKey);
        WhatsAppConversationMetadata? metadata = whatsAppContext is null
            ? null
            : new WhatsAppConversationMetadata
            {
                BusinessLabels = whatsAppContext.BusinessLabels,
                VerifiedBusinessName = whatsAppContext.VerifiedBusinessName,
                ProfilePhoneNumber = whatsAppContext.ProfilePhoneNumber,
                ContactPhoneNumber = whatsAppContext.ContactPhoneNumber,
                ChatJid = whatsAppContext.ConversationKey
            };

        return AiWhatsAppCopilotPromptService.BuildPrompt(
            instance?.DisplayName ?? instanceId,
            instance?.BranchKey,
            customerName ?? "Customer",
            lastInbound,
            messages,
            metadata,
            instanceId,
            conversationKey);
    }

    private static async Task<MessengerInstance?> TryLoadInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var registry = new InstanceRegistryService();
            await registry.LoadAsync(cancellationToken).ConfigureAwait(false);
            return registry.FindById(instanceId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Copilot instance lookup failed: {ex.Message}");
            return null;
        }
    }

    private static string ResolveConversationKey(string instanceId)
    {
        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .Where(candidate => candidate.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.LastMessageTime)
            .FirstOrDefault();

        return thread?.ConversationKey ?? string.Empty;
    }

    private static string BuildTranscriptHint(IReadOnlyList<ConversationMessageEntry> messages)
    {
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        var lines = messages
            .TakeLast(8)
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
        var script = $"window.__umExtractConversationContext({maxMessages});";

        try
        {
            var raw = await WebViewScriptGateway.Instance
                .ExecutePreparedScriptAsync(instanceId, script, cancellationToken)
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
