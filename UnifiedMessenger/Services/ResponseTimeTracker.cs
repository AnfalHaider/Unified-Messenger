using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Forward-tracked First Response Time (FRT) — the industry-standard messaging-support metric the app
/// previously couldn't report (the caught-up % is an unread proxy, not a speed measure).
///
/// WhatsApp Web's IndexedDB only exposes *current* state, not reply-latency history, so FRT is measured
/// going forward from real message timestamps observed across syncs: when a chat is seen awaiting a reply,
/// its inbound time is remembered as "pending"; when that chat is next seen replied (last message from us),
/// FRT = reply time − inbound time. Samples accrue over time — early data is sparse, which is honest.
///
/// Fed from <see cref="OversightChatSnapshotService.Update"/> (every Re-sync / background scan). Fully local.
/// </summary>
public sealed class ResponseTimeTracker
{
    /// <summary>Aggregate response-time stats over a set of accounts and a date window.</summary>
    public readonly record struct ResponseStats(
        bool HasData,
        int SampleCount,
        double MedianMinutes,
        double AverageMinutes,
        double P90Minutes,
        int SlaCompliancePercent,
        int AnsweredToday);

    // A reply taking longer than this is treated as a stale/abandoned thread, not a real response, so it
    // doesn't distort the median (e.g. a chat answered weeks later out-of-band).
    private static readonly TimeSpan MaxCredibleResponse = TimeSpan.FromDays(7);
    private const int MaxSamplesPerInstance = 1000;
    private static readonly TimeSpan SampleRetention = TimeSpan.FromDays(120);
    private const string FileName = "response-times.json";
    private const int SaveDebounceMilliseconds = 750;

    private static readonly Lazy<ResponseTimeTracker> LazyInstance = new(() => new ResponseTimeTracker());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ResponseTimeTracker Instance => LazyInstance.Value;

    // instanceId -> conversationKey -> inbound timestamp of the oldest unanswered customer message.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTimeOffset>> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    // instanceId -> recorded FRT samples (capped, pruned).
    private readonly ConcurrentDictionary<string, List<ResponseSample>> _samples =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;

    private ResponseTimeTracker()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal ResponseTimeTracker(string storePath)
    {
        _storePath = storePath;
    }

    /// <summary>
    /// Observe one chat's current state for an instance. Call for every chat on every snapshot. Detects the
    /// awaiting→answered transition and records an FRT sample. <paramref name="lastActivityUtc"/> is the chat's
    /// last-activity time, which is the inbound message time while awaiting and the reply time once replied.
    /// </summary>
    public void Observe(
        string instanceId,
        string conversationKey,
        bool isAwaiting,
        bool lastMessageFromMe,
        DateTimeOffset lastActivityUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(conversationKey))
        {
            return;
        }

        var id = instanceId.Trim();
        var pendingForInstance = _pending.GetOrAdd(id, _ => new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase));

        if (lastMessageFromMe)
        {
            // Replied. If we had a pending inbound for this chat, that's a completed response → record FRT.
            if (pendingForInstance.TryRemove(conversationKey, out var inboundUtc))
            {
                var frt = lastActivityUtc - inboundUtc;
                if (frt > TimeSpan.Zero && frt <= MaxCredibleResponse)
                {
                    RecordSample(id, lastActivityUtc, frt.TotalMinutes);
                }
            }

            return;
        }

        if (isAwaiting)
        {
            // Customer waiting. Remember the earliest unanswered inbound time (don't overwrite a prior one).
            pendingForInstance.TryAdd(conversationKey, lastActivityUtc);
        }
        // else: not awaiting and not confirmed-replied (ambiguous read) — leave any pending intact.
    }

    private void RecordSample(string instanceId, DateTimeOffset answeredAtUtc, double frtMinutes)
    {
        var list = _samples.GetOrAdd(instanceId, _ => []);
        lock (list)
        {
            list.Add(new ResponseSample(answeredAtUtc, frtMinutes));
            if (list.Count > MaxSamplesPerInstance)
            {
                list.RemoveRange(0, list.Count - MaxSamplesPerInstance);
            }
        }

        ScheduleSave();
    }

    /// <summary>
    /// Aggregate response-time stats across <paramref name="instances"/> for samples answered in the window.
    /// SLA compliance is computed against <paramref name="slaThresholdMinutes"/> so it tracks the current
    /// setting rather than a value baked in at record time. AnsweredToday counts samples answered today (local).
    /// </summary>
    public ResponseStats GetStats(
        IEnumerable<MessengerInstance> instances,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int slaThresholdMinutes)
    {
        var ids = ResolveInstanceIds(instances);
        var values = new List<double>();
        var withinSla = 0;
        var answeredToday = 0;
        var todayLocal = DateTime.Today;

        foreach (var id in ids)
        {
            if (!_samples.TryGetValue(id, out var list))
            {
                continue;
            }

            lock (list)
            {
                foreach (var sample in list)
                {
                    if (fromUtc is not null && sample.AnsweredAtUtc < fromUtc.Value)
                    {
                        continue;
                    }

                    if (toUtc is not null && sample.AnsweredAtUtc > toUtc.Value)
                    {
                        continue;
                    }

                    values.Add(sample.FrtMinutes);
                    if (slaThresholdMinutes <= 0 || sample.FrtMinutes <= slaThresholdMinutes)
                    {
                        withinSla++;
                    }

                    if (sample.AnsweredAtUtc.ToLocalTime().Date == todayLocal)
                    {
                        answeredToday++;
                    }
                }
            }
        }

        if (values.Count == 0)
        {
            return new ResponseStats(false, 0, 0, 0, 0, 0, answeredToday);
        }

        values.Sort();
        var median = Percentile(values, 0.50);
        var p90 = Percentile(values, 0.90);
        var average = values.Average();
        var slaPercent = (int)Math.Round(withinSla * 100.0 / values.Count);

        return new ResponseStats(true, values.Count, median, average, p90, slaPercent, answeredToday);
    }

    private static double Percentile(IReadOnlyList<double> sortedAscending, double fraction)
    {
        if (sortedAscending.Count == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(fraction * sortedAscending.Count) - 1;
        rank = Math.Clamp(rank, 0, sortedAscending.Count - 1);
        return sortedAscending[rank];
    }

    private static IReadOnlyList<string> ResolveInstanceIds(IEnumerable<MessengerInstance> instances) =>
        (instances ?? [])
        .Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Id))
        .Select(i => i.Id.Trim())
        .ToList();

    /// <summary>Loads persisted samples + in-flight pending waits. Idempotent.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            _isLoaded = true;
            if (!File.Exists(_storePath))
            {
                return;
            }

            ResponseStore? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer
                    .DeserializeAsync<ResponseStore>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Response-time store is corrupt; resetting: {ex.Message}");
                return;
            }

            var cutoff = DateTimeOffset.UtcNow - SampleRetention;
            foreach (var (instanceId, dto) in store?.Instances ?? [])
            {
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    continue;
                }

                if (dto.Samples is { Count: > 0 })
                {
                    var kept = dto.Samples
                        .Where(s => s.AnsweredAtUtc >= cutoff)
                        .Select(s => new ResponseSample(s.AnsweredAtUtc, s.FrtMinutes))
                        .ToList();
                    if (kept.Count > 0)
                    {
                        _samples[instanceId] = kept;
                    }
                }

                if (dto.Pending is { Count: > 0 })
                {
                    var pending = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (conv, inbound) in dto.Pending)
                    {
                        if (!string.IsNullOrWhiteSpace(conv))
                        {
                            pending[conv] = inbound;
                        }
                    }

                    _pending[instanceId] = pending;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Forces any pending debounced save to disk (call on suspend/exit).</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        lock (_debounceLock)
        {
            Interlocked.Increment(ref _saveGeneration);
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = null;
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ScheduleSave()
    {
        CancellationToken token;
        int generation;
        lock (_debounceLock)
        {
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = new CancellationTokenSource();
            token = _saveDebounceCts.Token;
            generation = Interlocked.Increment(ref _saveGeneration);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMilliseconds, token).ConfigureAwait(false);
                if (generation != Volatile.Read(ref _saveGeneration))
                {
                    return;
                }

                await SaveAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // debounced
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Response-time save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = new ResponseStore { Version = ResponseStore.CurrentVersion };
            var cutoff = DateTimeOffset.UtcNow - SampleRetention;

            foreach (var (instanceId, list) in _samples)
            {
                List<ResponseSampleDto> samples;
                lock (list)
                {
                    samples = list
                        .Where(s => s.AnsweredAtUtc >= cutoff)
                        .Select(s => new ResponseSampleDto { AnsweredAtUtc = s.AnsweredAtUtc, FrtMinutes = s.FrtMinutes })
                        .ToList();
                }

                store.Instances[instanceId] = new InstanceResponseDto { Samples = samples };
            }

            foreach (var (instanceId, pending) in _pending)
            {
                if (pending.IsEmpty)
                {
                    continue;
                }

                if (!store.Instances.TryGetValue(instanceId, out var dto))
                {
                    dto = new InstanceResponseDto();
                    store.Instances[instanceId] = dto;
                }

                dto.Pending = pending.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
            var tempPath = _storePath + ".tmp";
            await using (var stream = new FileStream(
                             tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                             bufferSize: 4096, options: FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _storePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private readonly record struct ResponseSample(DateTimeOffset AnsweredAtUtc, double FrtMinutes);

    private sealed class ResponseStore
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public Dictionary<string, InstanceResponseDto> Instances { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class InstanceResponseDto
    {
        public List<ResponseSampleDto>? Samples { get; set; }

        public Dictionary<string, DateTimeOffset>? Pending { get; set; }
    }

    private sealed class ResponseSampleDto
    {
        public DateTimeOffset AnsweredAtUtc { get; set; }

        public double FrtMinutes { get; set; }
    }
}
