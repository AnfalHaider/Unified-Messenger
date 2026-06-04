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
        _ = ProcessInboundAsync(selection);
    }

    internal async Task ProcessInboundAsync(
        InboundMessageSelection selection,
        CancellationToken cancellationToken = default)
    {
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

            var draftBuilder = new StringBuilder();
            await foreach (var token in OllamaOrchestrationService.Instance
                               .StreamGenerateAsync(
                                   prompt.UserPrompt,
                                   prompt.SystemPrompt,
                                   priority: InferencePriority.Background,
                                   cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
            {
                if (draftBuilder.Length + token.Length > WebViewDraftInjector.MaxDraftLength)
                {
                    break;
                }

                draftBuilder.Append(token);
            }

            var draft = draftBuilder.ToString().Trim();
            if (draft.Length < 4)
            {
                return;
            }

            var injected = await WebViewDraftInjector
                .InjectDraftAsync(selection.InstanceId, draft, cancellationToken)
                .ConfigureAwait(false);

            if (!injected)
            {
                return;
            }

            _lastSignatures[selection.InstanceId] = signature;
            _lastDraftAt[selection.InstanceId] = DateTimeOffset.UtcNow;

            DraftCompleted?.Invoke(
                this,
                new AutoDraftCompletedEventArgs(selection.InstanceId, selection.Platform, draft.Length));
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
        $"{selection.MessageText.Trim()}|{selection.ConversationHint.Trim()}";

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
