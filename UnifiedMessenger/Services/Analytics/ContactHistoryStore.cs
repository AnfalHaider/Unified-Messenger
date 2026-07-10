using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Tracks, per account, the first and last time each customer contact was seen — the basis for a
/// new-vs-returning-customer insight ("12 new · 34 returning this week"). Fed from every oversight scan
/// (<see cref="OversightChatSnapshotService.Update"/>). Contact identity is the phone number when known
/// (stable across saved/unsaved), else the conversation JID; groups / status / broadcast are ignored.
///
/// Honesty guard: "new" only becomes meaningful once the app has watched for a full week — before that,
/// every contact looks new because we've only just started observing. <see cref="GetInsight"/> reports
/// <c>HasEnoughHistory=false</c> until the earliest observation is ≥ 7 days old. Fully local.
/// </summary>
public sealed class ContactHistoryStore
{
    /// <summary>New vs returning customers active within a window, once enough history has accrued.</summary>
    public readonly record struct ContactInsight(
        bool HasEnoughHistory,
        int NewCount,
        int ReturningCount,
        int ActiveThisWeek,
        int ReturningRatePercent);

    private const string FileName = "contact-history.json";
    private const int SaveDebounceMilliseconds = 750;
    private const int MaxContactsPerInstance = 20000;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(180);
    private static readonly TimeSpan MinHistoryForInsight = TimeSpan.FromDays(7);

    private static readonly Lazy<ContactHistoryStore> LazyInstance = new(() => new ContactHistoryStore());

    public static ContactHistoryStore Instance => LazyInstance.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // instanceId -> contactKey -> (firstSeenUtc, lastSeenUtc).
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ContactSeen>> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;

    private ContactHistoryStore()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal ContactHistoryStore(string storePath)
    {
        _storePath = storePath;
    }

    /// <summary>
    /// Records one contact sighting for an account: sets first-seen (min) and last-seen (max). Groups,
    /// status, broadcast and newsletter conversations are ignored — only real customer contacts count.
    /// </summary>
    public void Observe(string instanceId, string conversationKey, string contactPhone, DateTimeOffset activityUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var contactKey = ResolveContactKey(conversationKey, contactPhone);
        if (contactKey is null)
        {
            return;
        }

        var id = instanceId.Trim();
        var forInstance = _byInstance.GetOrAdd(id, _ => new ConcurrentDictionary<string, ContactSeen>(StringComparer.OrdinalIgnoreCase));

        forInstance.AddOrUpdate(
            contactKey,
            _ => new ContactSeen(activityUtc, activityUtc),
            (_, existing) => new ContactSeen(
                activityUtc < existing.FirstSeenUtc ? activityUtc : existing.FirstSeenUtc,
                activityUtc > existing.LastSeenUtc ? activityUtc : existing.LastSeenUtc));

        ScheduleSave();
    }

    /// <summary>
    /// New vs returning customers across <paramref name="instanceIds"/> for the window [weekStart, now].
    /// New = first ever seen this window; returning = known before the window but active again in it.
    /// </summary>
    public ContactInsight GetInsight(IEnumerable<string> instanceIds, DateTimeOffset weekStartUtc, DateTimeOffset nowUtc)
    {
        var ids = (instanceIds ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .ToList();

        var earliest = DateTimeOffset.MaxValue;
        var newCount = 0;
        var returning = 0;
        var active = 0;

        foreach (var id in ids)
        {
            if (!_byInstance.TryGetValue(id, out var contacts))
            {
                continue;
            }

            foreach (var seen in contacts.Values)
            {
                if (seen.FirstSeenUtc < earliest)
                {
                    earliest = seen.FirstSeenUtc;
                }

                if (seen.LastSeenUtc < weekStartUtc)
                {
                    continue; // not active in the window
                }

                active++;
                if (seen.FirstSeenUtc >= weekStartUtc)
                {
                    newCount++;
                }
                else
                {
                    returning++;
                }
            }
        }

        var hasHistory = earliest != DateTimeOffset.MaxValue && nowUtc - earliest >= MinHistoryForInsight;
        var rate = active > 0 ? (int)Math.Round(returning * 100.0 / active) : 0;
        return new ContactInsight(hasHistory, newCount, returning, active, rate);
    }

    /// <summary>Contact identity: phone digits when known (stable across saved/unsaved), else the JID.
    /// Returns null for groups / status / broadcast / newsletter — those aren't individual customers.</summary>
    internal static string? ResolveContactKey(string? conversationKey, string? contactPhone)
    {
        var ck = conversationKey ?? string.Empty;
        if (ck.Contains("@g.us", StringComparison.OrdinalIgnoreCase) ||
            ck.Contains("@broadcast", StringComparison.OrdinalIgnoreCase) ||
            ck.Contains("@newsletter", StringComparison.OrdinalIgnoreCase) ||
            ck.StartsWith("status", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var digits = new string((contactPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length >= 6)
        {
            return "p:" + digits;
        }

        return string.IsNullOrWhiteSpace(ck) ? null : "k:" + ck;
    }

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

            ContactHistoryStoreDto? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer
                    .DeserializeAsync<ContactHistoryStoreDto>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Contact-history store is corrupt; resetting: {ex.Message}");
                return;
            }

            var cutoff = DateTimeOffset.UtcNow - Retention;
            foreach (var (instanceId, contacts) in store?.Instances ?? [])
            {
                if (string.IsNullOrWhiteSpace(instanceId) || contacts is null)
                {
                    continue;
                }

                var map = new ConcurrentDictionary<string, ContactSeen>(StringComparer.OrdinalIgnoreCase);
                foreach (var (contactKey, dto) in contacts)
                {
                    if (string.IsNullOrWhiteSpace(contactKey) || dto.LastSeenUtc < cutoff)
                    {
                        continue;
                    }

                    map[contactKey] = new ContactSeen(dto.FirstSeenUtc, dto.LastSeenUtc);
                }

                if (!map.IsEmpty)
                {
                    _byInstance[instanceId] = map;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

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
                Debug.WriteLine($"Contact-history save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = new ContactHistoryStoreDto();
            var cutoff = DateTimeOffset.UtcNow - Retention;

            foreach (var (instanceId, contacts) in _byInstance)
            {
                var kept = contacts
                    .Where(c => c.Value.LastSeenUtc >= cutoff)
                    .OrderByDescending(c => c.Value.LastSeenUtc)
                    .Take(MaxContactsPerInstance)
                    .ToDictionary(
                        c => c.Key,
                        c => new ContactSeenDto { FirstSeenUtc = c.Value.FirstSeenUtc, LastSeenUtc = c.Value.LastSeenUtc },
                        StringComparer.OrdinalIgnoreCase);

                if (kept.Count > 0)
                {
                    store.Instances[instanceId] = kept;
                }
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

    private readonly record struct ContactSeen(DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc);

    private sealed class ContactHistoryStoreDto
    {
        public Dictionary<string, Dictionary<string, ContactSeenDto>> Instances { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ContactSeenDto
    {
        public DateTimeOffset FirstSeenUtc { get; set; }

        public DateTimeOffset LastSeenUtc { get; set; }
    }
}
