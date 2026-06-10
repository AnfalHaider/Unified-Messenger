using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

/// <summary>
/// On-demand and post-backfill batch summarization for branch workspaces.
/// </summary>
public sealed class BranchPulseService
{
    private static readonly Lazy<BranchPulseService> LazyInstance = new(() => new BranchPulseService());

    private readonly object _gate = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    private BranchPulseService()
    {
    }

    public static BranchPulseService Instance => LazyInstance.Value;

    public event EventHandler<BranchPulseChangedEventArgs>? Changed;

    public BranchPulseSnapshot GetCached(string? branchKey)
    {
        if (TryGetCache(ResolveScopeKey(branchKey), out var entry))
        {
            return entry.Snapshot;
        }

        return new BranchPulseSnapshot
        {
            BranchKey = branchKey,
            ScopeLabel = BuildScopeLabel(branchKey),
            State = BranchPulseState.Idle,
            StatusMessage = "Run branch pulse to summarize open threads."
        };
    }

    public void Invalidate(string? branchKey = null)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(branchKey))
            {
                _cache.Clear();
                return;
            }

            _cache.Remove(ResolveScopeKey(branchKey));
        }
    }

    public void RequestRefreshAfterBackfill(MessengerInstance instance)
    {
        if (!AppSettingsService.Instance.Settings.EnableBranchPulse ||
            !AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            return;
        }

        var branchKey = BranchWorkspaceHelper.ResolveBranchKey(instance);
        Invalidate(branchKey);
        _ = GenerateAsync(branchKey, force: false);
    }

    public Task<BranchPulseSnapshot> GenerateAsync(
        string? branchKey,
        IReadOnlyList<ThreadData>? threads = null,
        IReadOnlyList<MessengerInstance>? instances = null,
        bool force = false) =>
        GenerateCoreAsync(branchKey, threads, instances, force, llmGenerateAsync: null);

    internal Task<BranchPulseSnapshot> GenerateForTestsAsync(
        string? branchKey,
        IReadOnlyList<ThreadData> threads,
        Func<string, string?, Task<string?>> llmGenerateAsync) =>
        GenerateCoreAsync(branchKey, threads, instances: null, force: true, llmGenerateAsync);

    private async Task<BranchPulseSnapshot> GenerateCoreAsync(
        string? branchKey,
        IReadOnlyList<ThreadData>? threads,
        IReadOnlyList<MessengerInstance>? instances,
        bool force,
        Func<string, string?, Task<string?>>? llmGenerateAsync)
    {
        var settings = AppSettingsService.Instance.Settings;
        var scopeLabel = BuildScopeLabel(branchKey);
        var scopeKey = ResolveScopeKey(branchKey);

        if (!settings.EnableBranchPulse || !settings.EnableLocalAi)
        {
            var disabled = new BranchPulseSnapshot
            {
                BranchKey = branchKey,
                ScopeLabel = scopeLabel,
                State = BranchPulseState.Disabled,
                StatusMessage = BranchPulseSnapshot.Disabled.StatusMessage
            };
            Publish(scopeKey, disabled);
            return disabled;
        }

        if (OllamaOrchestrationService.Instance.ConnectionState != OllamaConnectionState.Running &&
            llmGenerateAsync is null)
        {
            var unavailable = new BranchPulseSnapshot
            {
                BranchKey = branchKey,
                ScopeLabel = scopeLabel,
                State = BranchPulseState.Unavailable,
                StatusMessage = "Ollama is offline. Start Local AI to run branch pulse."
            };
            Publish(scopeKey, unavailable);
            return unavailable;
        }

        var openThreads = ResolveOpenThreads(branchKey, threads, instances);
        var fingerprint = ComputeFingerprint(openThreads);
        if (!force &&
            TryGetCache(scopeKey, out var cached) &&
            cached.Fingerprint == fingerprint &&
            cached.Snapshot.State == BranchPulseState.Ready)
        {
            return cached.Snapshot;
        }

        if (openThreads.Count == 0)
        {
            var empty = new BranchPulseSnapshot
            {
                BranchKey = branchKey,
                ScopeLabel = scopeLabel,
                OpenThreadCount = 0,
                State = BranchPulseState.NoThreads,
                StatusMessage = "No open threads in this branch scope."
            };
            Publish(scopeKey, empty, fingerprint);
            return empty;
        }

        lock (_gate)
        {
            if (_inFlight.Contains(scopeKey))
            {
                return TryGetCache(scopeKey, out cached)
                    ? cached.Snapshot
                    : new BranchPulseSnapshot
                    {
                        BranchKey = branchKey,
                        ScopeLabel = scopeLabel,
                        OpenThreadCount = openThreads.Count,
                        State = BranchPulseState.Generating,
                        StatusMessage = "Generating branch pulse…"
                    };
            }

            _inFlight.Add(scopeKey);
        }

        var generating = new BranchPulseSnapshot
        {
            BranchKey = branchKey,
            ScopeLabel = scopeLabel,
            OpenThreadCount = openThreads.Count,
            State = BranchPulseState.Generating,
            StatusMessage = "Summarizing open threads…"
        };
        Publish(scopeKey, generating, fingerprint);

        try
        {
            var batch = AiBranchPulsePromptService.SelectThreadsForBatch(openThreads);
            var prompt = AiBranchPulsePromptService.BuildUserPrompt(scopeLabel, branchKey, batch);
            var raw = llmGenerateAsync is not null
                ? await llmGenerateAsync(prompt, AiBranchPulsePromptService.SystemPrompt).ConfigureAwait(false)
                : await OllamaInferenceCoordinator.Instance
                    .CollectGenerateAsync(
                        InferencePriority.Background,
                        prompt,
                        AiBranchPulsePromptService.SystemPrompt,
                        modelOverride: settings.LocalAiModelName)
                    .ConfigureAwait(false);

            var parsed = AiBranchPulsePromptService.ParseResponse(
                scopeLabel,
                branchKey,
                openThreads.Count,
                raw);
            Publish(scopeKey, parsed, fingerprint);
            return parsed;
        }
        catch (Exception ex)
        {
            var failed = new BranchPulseSnapshot
            {
                BranchKey = branchKey,
                ScopeLabel = scopeLabel,
                OpenThreadCount = openThreads.Count,
                State = BranchPulseState.Error,
                StatusMessage = $"Branch pulse failed: {ex.Message}"
            };
            Publish(scopeKey, failed, fingerprint);
            return failed;
        }
        finally
        {
            lock (_gate)
            {
                _inFlight.Remove(scopeKey);
            }
        }
    }

    private static IReadOnlyList<ThreadData> ResolveOpenThreads(
        string? branchKey,
        IReadOnlyList<ThreadData>? threads,
        IReadOnlyList<MessengerInstance>? instances)
    {
        var source = threads ?? ThreadRegistryService.Instance.GetAllThreads();
        IEnumerable<ThreadData> scoped = source;

        if (instances is { Count: > 0 })
        {
            var allowedIds = instances
                .Select(instance => instance.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            scoped = scoped.Where(thread => allowedIds.Contains(thread.InstanceId));
        }

        if (!string.IsNullOrWhiteSpace(branchKey))
        {
            scoped = scoped.Where(thread =>
                thread.BranchName.Equals(branchKey.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return scoped
            .Where(thread => !thread.IsReplied && !thread.IsSpamOrPromo)
            .ToList();
    }

    private static string ComputeFingerprint(IReadOnlyList<ThreadData> openThreads)
    {
        if (openThreads.Count == 0)
        {
            return "empty";
        }

        var parts = openThreads
            .OrderBy(thread => thread.ThreadId, StringComparer.OrdinalIgnoreCase)
            .Select(thread =>
                $"{thread.ThreadId}:{thread.UrgencyScore}:{thread.LastMessageTime.UtcTicks}:{thread.NextActionSummary.Length}");
        return string.Join('|', parts);
    }

    private static string ResolveScopeKey(string? branchKey) =>
        string.IsNullOrWhiteSpace(branchKey) ? "__all__" : branchKey.Trim();

    private static string BuildScopeLabel(string? branchKey) =>
        string.IsNullOrWhiteSpace(branchKey) ? "All branches" : branchKey.Trim();

    private bool TryGetCache(string scopeKey, out CacheEntry entry)
    {
        lock (_gate)
        {
            return _cache.TryGetValue(scopeKey, out entry!);
        }
    }

    private void Publish(string scopeKey, BranchPulseSnapshot snapshot, string? fingerprint = null)
    {
        lock (_gate)
        {
            _cache[scopeKey] = new CacheEntry(snapshot, fingerprint ?? ComputeFingerprint([]));
        }

        Changed?.Invoke(this, new BranchPulseChangedEventArgs(snapshot));
    }

    private sealed record CacheEntry(BranchPulseSnapshot Snapshot, string Fingerprint);
}

public sealed class BranchPulseChangedEventArgs : EventArgs
{
    public BranchPulseChangedEventArgs(BranchPulseSnapshot snapshot) => Snapshot = snapshot;

    public BranchPulseSnapshot Snapshot { get; }
}
