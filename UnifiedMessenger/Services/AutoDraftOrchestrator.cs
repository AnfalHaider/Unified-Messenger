using System.Collections.Concurrent;
using System.Text;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

public sealed class AutoDraftOrchestrator
{
    private const int MinMessageLength = 8;

    private const int DuplicateWindowSeconds = 20;

    private static readonly Lazy<AutoDraftOrchestrator> LazyInstance = new(() => new AutoDraftOrchestrator());

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _instanceGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _lastSignatures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastDraftAt = new(StringComparer.OrdinalIgnoreCase);

    public static AutoDraftOrchestrator Instance => LazyInstance.Value;

    public event EventHandler<AutoDraftCompletedEventArgs>? DraftCompleted;

    public void HandleInboundMessage(InboundMessageSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!PlatformModules.PlatformModuleRegistry.Instance.IsEnabled(selection.Platform))
        {
            return;
        }

        _ = ProcessInboundAsync(selection);
    }

    public void HandleTriageDraftReady(MessageTriageItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!PlatformModules.PlatformModuleRegistry.Instance.IsEnabled(item.Platform))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.SuggestedDraftResponse))
        {
            return;
        }

        var messageText = string.IsNullOrWhiteSpace(item.MessageFullText)
            ? item.MessagePreview
            : item.MessageFullText;
        _ = TryInjectTriageDraftAsync(
            item.InstanceId,
            item.Platform,
            item.SuggestedDraftResponse,
            messageText,
            item.ConversationKey);
    }

    public async Task<bool> TryInjectTriageDraftAsync(
        string instanceId,
        string platform,
        string draftText,
        string messageText,
        string conversationHint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            string.IsNullOrWhiteSpace(platform) ||
            string.IsNullOrWhiteSpace(draftText))
        {
            return false;
        }

        if (!PlatformModules.PlatformModuleRegistry.Instance.IsEnabled(platform))
        {
            return false;
        }

        var settings = AppSettingsService.Instance.Settings;
        if (!settings.EnableLocalAi || !settings.EnableAutoDraft)
        {
            return false;
        }

        if (settings.AutoDraftOnlyWhenVisible &&
            !ActiveWorkspaceContext.IsInstanceActive(instanceId))
        {
            return false;
        }

        var trimmedDraft = draftText.Trim();
        if (trimmedDraft.Length < 4)
        {
            return false;
        }

        var signature = BuildTriageSignature(messageText, conversationHint);
        if (IsDuplicate(instanceId, signature))
        {
            return false;
        }

        var instanceGate = _instanceGates.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
        await instanceGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsDuplicate(instanceId, signature))
            {
                return false;
            }

            var injected = await WebViewDraftInjector
                .InjectDraftAsync(instanceId, trimmedDraft, cancellationToken)
                .ConfigureAwait(false);

            if (!injected)
            {
                return false;
            }

            _lastSignatures[instanceId] = signature;
            _lastDraftAt[instanceId] = DateTimeOffset.UtcNow;

            DraftCompleted?.Invoke(
                this,
                new AutoDraftCompletedEventArgs(instanceId, platform, trimmedDraft.Length));

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Triage draft injection failed for {instanceId}: {ex.Message}");
            return false;
        }
        finally
        {
            instanceGate.Release();
        }
    }

    internal async Task ProcessInboundAsync(
        InboundMessageSelection selection,
        CancellationToken cancellationToken = default)
    {
        if (!PlatformModules.PlatformModuleRegistry.Instance.IsEnabled(selection.Platform))
        {
            return;
        }

        var settings = AppSettingsService.Instance.Settings;
        if (!settings.EnableLocalAi || !settings.EnableAutoDraft)
        {
            return;
        }

        if (selection.MessageText.Trim().Length < MinMessageLength)
        {
            return;
        }

        if (settings.AutoDraftOnlyWhenVisible &&
            !ActiveWorkspaceContext.IsInstanceActive(selection.InstanceId))
        {
            return;
        }

        var signature = BuildSignature(selection);
        if (IsDuplicate(selection.InstanceId, signature))
        {
            return;
        }

        var instanceGate = _instanceGates.GetOrAdd(selection.InstanceId, _ => new SemaphoreSlim(1, 1));
        if (!await instanceGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var prompt = AiDraftPromptService.BuildPrompt(
                selection.Platform,
                selection.MessageText,
                selection.CustomerName,
                selection.ConversationHint);

            await WebViewDraftInjector.ResetDraftStreamAsync(selection.InstanceId, cancellationToken)
                .ConfigureAwait(false);

            var pending = new StringBuilder();
            var totalChars = 0;
            var lastFlush = Environment.TickCount64;

            await foreach (var token in OllamaOrchestrationService.Instance
                               .StreamGenerateAsync(
                                   prompt.UserPrompt,
                                   prompt.SystemPrompt,
                                   priority: InferencePriority.Background,
                                   cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
            {
                if (totalChars + token.Length > WebViewDraftInjector.MaxDraftLength)
                {
                    break;
                }

                pending.Append(token);
                totalChars += token.Length;
                var now = Environment.TickCount64;
                if (now - lastFlush < DraftStreamFlushHelper.DefaultFlushIntervalMs)
                {
                    continue;
                }

                await DraftStreamFlushHelper
                    .FlushPendingAsync(selection.InstanceId, pending, cancellationToken)
                    .ConfigureAwait(false);
                lastFlush = now;
            }

            await DraftStreamFlushHelper
                .FlushPendingAsync(selection.InstanceId, pending, cancellationToken)
                .ConfigureAwait(false);

            if (totalChars < 4)
            {
                return;
            }

            var injected = await WebViewDraftInjector
                .FinalizeDraftStreamAsync(selection.InstanceId, cancellationToken)
                .ConfigureAwait(false);

            if (!injected)
            {
                return;
            }

            _lastSignatures[selection.InstanceId] = signature;
            _lastDraftAt[selection.InstanceId] = DateTimeOffset.UtcNow;

            DraftCompleted?.Invoke(
                this,
                new AutoDraftCompletedEventArgs(selection.InstanceId, selection.Platform, totalChars));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Auto-draft failed for {selection.InstanceId}: {ex.Message}");
        }
        finally
        {
            instanceGate.Release();
        }
    }

    private static string BuildSignature(InboundMessageSelection selection) =>
        BuildTriageSignature(selection.MessageText, selection.ConversationHint);

    internal static string BuildTriageSignature(string messageText, string conversationHint) =>
        $"{messageText.Trim()}|{conversationHint.Trim()}";

    private bool IsDuplicate(string instanceId, string signature)
    {
        if (!_lastSignatures.TryGetValue(instanceId, out var previous) ||
            !string.Equals(previous, signature, StringComparison.Ordinal))
        {
            return false;
        }

        return _lastDraftAt.TryGetValue(instanceId, out var lastAt) &&
               DateTimeOffset.UtcNow - lastAt < TimeSpan.FromSeconds(DuplicateWindowSeconds);
    }
}

public sealed class AutoDraftCompletedEventArgs : EventArgs
{
    public AutoDraftCompletedEventArgs(string instanceId, string platform, int draftLength)
    {
        InstanceId = instanceId;
        Platform = platform;
        DraftLength = draftLength;
    }

    public string InstanceId { get; }

    public string Platform { get; }

    public int DraftLength { get; }
}
