namespace UnifiedMessenger.Services.Ai;

/// <summary>The outcome of asking the local model for a reply draft.</summary>
/// <param name="Text">The reply, when <paramref name="Verdict"/> is <see cref="DraftVerdict.Ok"/>.</param>
/// <param name="Message">What to tell the owner when there is no draft.</param>
public readonly record struct ReplyDraftResult(DraftVerdict Verdict, string Text, string Message)
{
    public bool HasDraft => Verdict == DraftVerdict.Ok && !string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// Drafts a public reply to one Google review with the on-device model, on demand.
/// </summary>
/// <remarks>
/// <para>
/// <b>On demand, not in the background.</b> Unlike the insight lines, nothing is generated until the owner
/// asks for a specific review. Drafting every waiting review speculatively would put every customer's words
/// through the model for replies that mostly never get used.
/// </para>
/// <para>
/// <b>The app still never sends.</b> This returns text. The desk puts it on the clipboard and opens Google's
/// own reply box; the owner reads, edits and sends. See <see cref="ReviewReplyDraft"/> for the guardrails on
/// what is allowed to reach them at all.
/// </para>
/// <para>
/// <b>Where the review text goes.</b> To the Ollama endpoint, which is localhost. Reviews are already public
/// writing, but the rule is the same as everywhere else in this app: nothing leaves the machine.
/// </para>
/// </remarks>
public sealed class ReviewReplyService
{
    private static readonly Lazy<ReviewReplyService> LazyInstance = new(() => new ReviewReplyService());

    public static ReviewReplyService Instance => LazyInstance.Value;

    private readonly Func<bool> _aiEnabledProvider;
    private readonly OllamaRuntimeService _runtime;
    private readonly OllamaInferenceClient _client;
    private readonly Func<string> _modelProvider;

    internal ReviewReplyService(
        Func<bool>? aiEnabledProvider = null,
        OllamaRuntimeService? runtime = null,
        OllamaInferenceClient? client = null,
        Func<string>? modelProvider = null)
    {
        _aiEnabledProvider = aiEnabledProvider ?? (() => AppSettingsService.Instance.Settings.EnableLocalAi);
        _runtime = runtime ?? OllamaRuntimeService.Instance;
        _client = client ?? OllamaInferenceClient.Instance;
        _modelProvider = modelProvider ?? (() =>
        {
            var configured = AppSettingsService.Instance.Settings.LocalAiModelName;
            return string.IsNullOrWhiteSpace(configured) ? OllamaOptions.DefaultModelName : configured.Trim();
        });
    }

    /// <summary>Whether the owner has local AI switched on at all.</summary>
    /// <remarks>
    /// Drives whether the desk offers a draft button. Offering one that always fails is worse than not
    /// offering it, so the button is only shown when this is true.
    /// </remarks>
    public bool IsEnabled => _aiEnabledProvider();

    /// <summary>Asks the model for a reply, and applies the guardrails before returning it.</summary>
    public async Task<ReplyDraftResult> DraftAsync(
        QueuedReview review,
        string businessName,
        CancellationToken cancellationToken = default)
    {
        if (!_aiEnabledProvider())
        {
            return new ReplyDraftResult(
                DraftVerdict.Empty, string.Empty,
                "Local AI is off. Turn it on in Settings to draft replies.");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(OllamaOptions.InferenceTimeout);

            if (!await _runtime.EnsureRunningAsync(cts.Token).ConfigureAwait(false))
            {
                return new ReplyDraftResult(
                    DraftVerdict.Empty, string.Empty,
                    "The local AI runtime isn't running, so no draft could be written.");
            }

            // The runtime being up and the model being present are different things. Found live: Ollama
            // running with nothing pulled produced only "No draft could be written", which tells the owner
            // nothing they can act on. Naming the missing piece turns a dead end into one click.
            var model = _modelProvider();
            if (!await _client.IsModelInstalledAsync(model, cts.Token).ConfigureAwait(false))
            {
                return new ReplyDraftResult(
                    DraftVerdict.Empty, string.Empty,
                    $"The AI model ({model}) hasn't been downloaded yet — Settings → AI to get it.");
            }

            var raw = await _client
                .GenerateTextAsync(
                    ReviewReplyDraft.BuildPrompt(review, businessName),
                    ReviewReplyDraft.SystemPrompt,
                    _modelProvider(),
                    cts.Token)
                .ConfigureAwait(false);

            var verdict = ReviewReplyDraft.Validate(raw, out var cleaned);
            return verdict == DraftVerdict.Ok
                ? new ReplyDraftResult(verdict, cleaned, string.Empty)
                : new ReplyDraftResult(verdict, string.Empty, ReviewReplyDraft.ExplainRefusal(verdict));
        }
        catch (OperationCanceledException)
        {
            return new ReplyDraftResult(
                DraftVerdict.Empty, string.Empty, "The draft took too long and was stopped.");
        }
        catch (Exception ex)
        {
            // A failed draft is an inconvenience, never a crash: the owner writes the reply themselves.
            AppLogger.LogWarning("ReviewReply", $"Draft failed: {ex.GetType().Name}: {ex.Message}");
            return new ReplyDraftResult(
                DraftVerdict.Empty, string.Empty, "No draft could be written. Write this one yourself.");
        }
    }
}
