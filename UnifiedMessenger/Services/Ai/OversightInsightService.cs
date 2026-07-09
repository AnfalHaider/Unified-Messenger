using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using UnifiedMessenger.Models.Ai;

namespace UnifiedMessenger.Services.Ai;

/// <summary>
/// Facts about one account/location that need attention, fed to the local model to phrase a one-line insight.
/// Counts only — never customer names or message content — so nothing sensitive leaves the device beyond
/// aggregate numbers (and the model is on-device regardless).
/// </summary>
public sealed record OversightInsightFacts(
    string AccountName,
    int AwaitingCount,
    int UnreadCount,
    int CaughtUpPercent,
    string OldestWaitText);

/// <summary>
/// Optional local-AI enhancement for the command-center insight strips. When <c>EnableLocalAi</c> is on and
/// the Ollama runtime is reachable, this generates a short, natural-language attention line per account and
/// caches it. Generation is fire-and-forget, deduped by a state signature, and serialized so a burst of
/// accounts doesn't hammer the runtime. Everything degrades silently to the caller's heuristic on failure.
/// </summary>
public sealed class OversightInsightService
{
    private static readonly Lazy<OversightInsightService> LazyInstance = new(() => new OversightInsightService());

    private const string SystemPrompt =
        "You are an operations assistant for a multi-location business owner who monitors WhatsApp customer " +
        "chats. You are given only aggregate counts for ONE account. Reply with EXACTLY ONE short line " +
        "(max 18 words) telling the owner what needs attention and one concrete next step. Plain sentence, " +
        "no greeting, no markdown, no quotes. Never invent customer names or message text.";

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateLock = new();

    private readonly Func<bool> _aiEnabledProvider;
    private readonly OllamaRuntimeService _runtime;
    private readonly OllamaInferenceClient _client;
    private readonly Func<string> _modelProvider;

    internal OversightInsightService(
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

    public static OversightInsightService Instance => LazyInstance.Value;

    /// <summary>Returns the cached AI line for this account if it matches the current state signature.</summary>
    public string? TryGet(string entityKey, string signature)
    {
        if (string.IsNullOrWhiteSpace(entityKey))
        {
            return null;
        }

        return _cache.TryGetValue(entityKey, out var entry) && entry.Signature == signature
            ? entry.Text
            : null;
    }

    /// <summary>
    /// Fire-and-forget: if AI is enabled and there's no current cached line for this exact state, generate one
    /// in the background and invoke <paramref name="onReady"/> (on a thread-pool thread) when it lands.
    /// No-ops when AI is off, already cached for this signature, or already generating for this account.
    /// </summary>
    public void Request(string entityKey, string signature, OversightInsightFacts facts, Action onReady) =>
        Request(entityKey, signature, BuildPrompt(facts), SystemPrompt, onReady);

    /// <summary>
    /// General narration: generate a cached one-liner from an arbitrary user/system prompt pair (used by the
    /// shift briefing and other aggregate narrations). Same dedup/cache/degrade semantics as the facts overload.
    /// </summary>
    public void Request(string entityKey, string signature, string userPrompt, string systemPrompt, Action onReady)
    {
        if (string.IsNullOrWhiteSpace(entityKey) || !_aiEnabledProvider())
        {
            return;
        }

        if (TryGet(entityKey, signature) is not null)
        {
            return;
        }

        lock (_stateLock)
        {
            if (!_inFlight.Add(entityKey))
            {
                return;
            }
        }

        _ = GenerateAsync(entityKey, signature, userPrompt, systemPrompt, onReady);
    }

    private async Task GenerateAsync(string entityKey, string signature, string userPrompt, string systemPrompt, Action onReady)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // State may have changed (and been re-requested) while queued — re-check the dedup cache.
            if (TryGet(entityKey, signature) is not null)
            {
                return;
            }

            using var cts = new CancellationTokenSource(OllamaOptions.InferenceTimeout);

            if (!_aiEnabledProvider() || !await _runtime.EnsureRunningAsync(cts.Token).ConfigureAwait(false))
            {
                return;
            }

            var text = await _client
                .GenerateTextAsync(userPrompt, systemPrompt, _modelProvider(), cts.Token)
                .ConfigureAwait(false);

            text = Sanitize(text);
            if (text is null)
            {
                return;
            }

            _cache[entityKey] = new CacheEntry(signature, text);
            onReady();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Oversight insight generation failed: {ex.Message}");
        }
        finally
        {
            lock (_stateLock)
            {
                _inFlight.Remove(entityKey);
            }

            _gate.Release();
        }
    }

    internal static string BuildPrompt(OversightInsightFacts f)
    {
        var unread = f.UnreadCount > 0 ? $"{f.UnreadCount} of them unread" : "all already opened but not yet replied";
        return
            $"Account: {f.AccountName}. {f.AwaitingCount} customer(s) waiting on a reply ({unread}). " +
            $"Longest wait: {f.OldestWaitText}. Currently {f.CaughtUpPercent}% caught up. " +
            "Write the one-line attention summary.";
    }

    /// <summary>
    /// Trim the model's output to a single tidy line: collapse whitespace, drop a leading label, strip
    /// surrounding and interior quotes, keep only the first sentence/clause (so trailing "Next action
    /// steps: …" run-ons are dropped), and enforce hard word/character backstops since the model routinely
    /// ignores the prompt's length limit.
    /// </summary>
    internal static string? Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // Collapse all whitespace to single spaces.
        var line = Regex.Replace(raw, @"\s+", " ").Trim();

        // Drop a leading label the model sometimes prepends ("Insight:", "Summary -", …).
        line = Regex.Replace(
            line, @"^(insight|summary|answer|attention|note|action)\s*[:\-]\s*", string.Empty,
            RegexOptions.IgnoreCase);

        // Strip surrounding quotes/markers.
        line = line.Trim().Trim('"', '\'', '*', '-', '•', ' ');

        // Keep only the first sentence/clause: cut at the first . ! ? ; past a small offset (so an early
        // abbreviation doesn't truncate it). This is what drops the trailing "Next action steps: …" run-on,
        // even when the period is immediately followed by a quote rather than a space.
        for (var i = 20; i < line.Length; i++)
        {
            if (line[i] is '.' or '!' or '?' or ';')
            {
                line = line[i] == ';' ? line[..i].TrimEnd() + "." : line[..(i + 1)];
                break;
            }
        }

        // Remove any remaining interior straight quotes the model left behind.
        line = line.Replace("\"", string.Empty).Trim();

        // Hard word-count backstop — the model often ignores the prompt's word limit.
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 26)
        {
            line = string.Join(' ', words.Take(26)).TrimEnd('.', ',', ';', ':', ' ') + "…";
        }

        // Hard character backstop for pathological single-token output.
        if (line.Length > 200)
        {
            line = line[..200].TrimEnd() + "…";
        }

        return string.IsNullOrWhiteSpace(line) ? null : line;
    }

    internal void ClearForTests() => _cache.Clear();

    private readonly record struct CacheEntry(string Signature, string Text);
}
