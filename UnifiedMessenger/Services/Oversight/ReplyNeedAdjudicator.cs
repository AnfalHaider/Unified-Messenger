using System.Collections.Concurrent;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Services;

/// <summary>
/// Asks the local model about the conversations the rules could not settle.
///
/// <para>
/// <b>Why only the middle.</b> The deterministic lexicon in <see cref="ReplyNeed"/> handles the clear
/// cases — "ok", "shukriya", a thumbs-up on one side; anything containing "kitna" or a question mark on
/// the other — and it does so with no model, no latency and a test suite. On the owner's real data it
/// settled 104 of 466 outright and correctly kept 41 questions. What it cannot judge is the roughly 115
/// messages in between: "Mel to mel", "Both signature and senior artist", "I fill the form". Those are
/// where a model earns its place, and running one over all 3,456 chats to reach them would be slow for
/// no gain.
/// </para>
/// <para>
/// <b>Fail-open, always.</b> Ollama being off, slow, missing the model, or answering something
/// unparseable all produce the same result: the conversation stays in the count. A feature that silently
/// shrinks the owner's queue when a background service is down would be worse than the bug it replaces.
/// </para>
/// <para>
/// <b>Privacy.</b> Ollama is localhost, which is what makes sending message text acceptable here at all —
/// the same basis on which <c>TranscriptBuilder</c> already sends a customer name and 800 characters of
/// body. This sends less: one message, no name, no phone number, capped at
/// <see cref="MaxPromptCharacters"/>. Nothing leaves the machine.
/// </para>
/// </summary>
public sealed class ReplyNeedAdjudicator
{
    /// <summary>How much of a message the model is shown. Enough to judge intent, no more.</summary>
    internal const int MaxPromptCharacters = 300;

    private const string SystemPrompt =
        "You decide whether a business owner still owes a customer a reply on WhatsApp. " +
        "The customer sent the last message. Messages are often in Roman Urdu, Urdu or a mix with English. " +
        "Answer with exactly one word: REPLY if the customer asked something, requested something, " +
        "raised a problem, or is clearly waiting for an answer. DONE if the message only acknowledges, " +
        "thanks, confirms, greets back, or otherwise closes the conversation. " +
        "If you are not sure, answer REPLY.";

    private static readonly Lazy<ReplyNeedAdjudicator> LazyInstance = new(() => new ReplyNeedAdjudicator());

    public static ReplyNeedAdjudicator Instance => LazyInstance.Value;

    // Keyed by the message text itself: the same words always get the same answer, and "ok" typed by
    // forty different customers costs one inference rather than forty.
    private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    // A delegate rather than IAiInferenceClient: that interface exposes only structured generation, and
    // this needs the free-text call. Taking the one function it uses also means the tests can drive it
    // with a canned answer instead of standing up a model.
    internal delegate Task<string?> CompletionDelegate(
        string prompt, string systemPrompt, string modelName, CancellationToken cancellationToken);

    private readonly CompletionDelegate _complete;
    private readonly Func<bool> _isAvailable;
    private readonly Func<string> _modelName;

    private ReplyNeedAdjudicator()
        : this(
            OllamaInferenceClient.Instance.GenerateTextAsync,
            () => AppSettingsService.Instance.Settings.EnableLocalAi &&
                  AppSettingsService.Instance.Settings.UseAiForReplyNeed,
            () => AppSettingsService.Instance.Settings.LocalAiModelName)
    {
    }

    internal ReplyNeedAdjudicator(CompletionDelegate complete, Func<bool> isAvailable, Func<string> modelName)
    {
        _complete = complete;
        _isAvailable = isAvailable;
        _modelName = modelName;
    }

    /// <summary>Number of distinct messages the model has ruled on. Surfaced in Settings.</summary>
    public int CachedVerdictCount => _cache.Count;

    /// <summary>
    /// The model's cached answer for this message, or <see langword="null"/> if it has not ruled on it.
    ///
    /// <para>
    /// Deliberately non-blocking. The dashboard renders from whatever is already known and never waits on
    /// inference — a queue that stalls behind a language model is not an improvement on a wrong number.
    /// Call <see cref="RequestAsync"/> to fill the cache in the background.
    /// </para>
    /// </summary>
    public bool? TryGetNeedsReply(string? preview)
    {
        var key = Normalize(preview);
        return key is not null && _cache.TryGetValue(key, out var needsReply) ? needsReply : null;
    }

    /// <summary>
    /// Whether this message is one the model should be asked about at all — i.e. the rules produced
    /// <see cref="ReplyNeedReason.Substantive"/>, which is the honest name for "unrecognised".
    /// </summary>
    public static bool IsAmbiguous(string? preview) =>
        ReplyNeed.Classify(preview).Reason == ReplyNeedReason.Substantive;

    /// <summary>
    /// Asks the model about every ambiguous message it has not already ruled on. Returns how many new
    /// verdicts were cached. Safe to call repeatedly; safe to call with Ollama off.
    /// </summary>
    public async Task<int> RequestAsync(IEnumerable<string?> previews, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previews);

        if (!_isAvailable())
        {
            return 0;
        }

        var model = _modelName();
        if (string.IsNullOrWhiteSpace(model))
        {
            return 0;
        }

        var pending = new List<string>();
        foreach (var preview in previews)
        {
            var key = Normalize(preview);
            if (key is null || _cache.ContainsKey(key) || !IsAmbiguous(preview))
            {
                continue;
            }

            if (_inFlight.TryAdd(key, 1))
            {
                pending.Add(key);
            }
        }

        var decided = 0;
        try
        {
            foreach (var key in pending)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var verdict = await AskAsync(key, model, cancellationToken).ConfigureAwait(false);
                if (verdict is not { } needsReply)
                {
                    continue; // no answer, no cache entry — it stays counted and can be retried later
                }

                _cache[key] = needsReply;
                decided++;
            }
        }
        finally
        {
            foreach (var key in pending)
            {
                _inFlight.TryRemove(key, out _);
            }
        }

        if (decided > 0)
        {
            AppLogger.LogInfo(
                "ReplyNeed.Ai",
                $"Local model ruled on {decided} previously-ambiguous message(s); {_cache.Count} cached.");
        }

        return decided;
    }

    private async Task<bool?> AskAsync(string message, string model, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await _complete(
                    $"Customer's last message:\n{message}", SystemPrompt, model, cancellationToken)
                .ConfigureAwait(false);

            return ParseAnswer(answer);
        }
        catch (Exception ex)
        {
            // The client already returns null rather than throwing, but a background task that dies
            // unobserved is exactly the failure mode the updater had. Log and keep the chat counted.
            AppLogger.LogWarning("ReplyNeed.Ai", $"Adjudication failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads the model's answer. Anything that is not an unambiguous "DONE" means the chat keeps its
    /// place — a model that rambles, hedges, or answers in a language of its own choosing must not be
    /// able to clear someone's queue.
    /// </summary>
    internal static bool? ParseAnswer(string? answer)
    {
        var text = (answer ?? string.Empty).Trim().Trim('.', '!', '"', '\'', '*');
        if (text.Length == 0)
        {
            return null;
        }

        // Take the first word only. Small models routinely answer "DONE - the customer is just saying
        // thanks", and the reasoning after the verdict must not be scanned for the other keyword.
        var firstSpace = text.IndexOfAny([' ', '\n', '\r', '\t', ',', ':', ';', '-']);
        var head = (firstSpace > 0 ? text[..firstSpace] : text).Trim();

        if (head.Equals("DONE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (head.Equals("REPLY", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return null;
    }

    /// <summary>Cache key: the trimmed, length-capped message. Null when there is nothing to judge.</summary>
    internal static string? Normalize(string? preview)
    {
        var text = (preview ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= MaxPromptCharacters ? text : text[..MaxPromptCharacters];
    }

    /// <summary>Drops every cached verdict. Used when the owner changes model or turns the feature off.</summary>
    public void Clear() => _cache.Clear();
}
