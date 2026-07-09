using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Manual overrides that suppress a chat from the "awaiting a reply" lists — for when the owner handled a
/// customer <b>outside</b> WhatsApp Web (on their phone, by phone call, in person) or wants to snooze it.
/// Two kinds, both self-expiring so the backlog can't be permanently faked:
/// <list type="bullet">
///   <item><b>Handled</b>: suppress while the chat's last activity is unchanged. If a NEW customer message
///   arrives (last activity moves past the recorded time) the chat re-appears — they need a reply again.</item>
///   <item><b>Snoozed</b>: suppress until a wall-clock time, then it re-appears regardless.</item>
/// </list>
/// Keyed by (instanceId, conversationKey). Persisted locally; pruned as overrides expire.
/// </summary>
public sealed class AwaitingOverrideStore
{
    private const string FileName = "awaiting-overrides.json";
    private const int SaveDebounceMilliseconds = 500;

    private static readonly Lazy<AwaitingOverrideStore> LazyInstance = new(() => new AwaitingOverrideStore());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AwaitingOverrideStore Instance => LazyInstance.Value;

    // instanceId -> conversationKey -> override.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Override>> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;

    /// <summary>Raised when an override is added/cleared, so the dashboard can re-render immediately.</summary>
    public event EventHandler? Changed;

    private AwaitingOverrideStore()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal AwaitingOverrideStore(string storePath)
    {
        _storePath = storePath;
    }

    /// <summary>Mark a chat handled elsewhere: suppressed until a newer customer message arrives.</summary>
    public void MarkHandled(string instanceId, string conversationKey, DateTimeOffset lastActivityUtc)
    {
        Set(instanceId, conversationKey, new Override(OverrideKind.Handled, lastActivityUtc, null));
    }

    /// <summary>Snooze a chat: suppressed until <paramref name="untilUtc"/>, then it re-appears.</summary>
    public void Snooze(string instanceId, string conversationKey, DateTimeOffset untilUtc)
    {
        Set(instanceId, conversationKey, new Override(OverrideKind.Snoozed, null, untilUtc));
    }

    /// <summary>Remove any override for a chat (it re-appears immediately if still awaiting).</summary>
    public void Clear(string instanceId, string conversationKey)
    {
        if (_byInstance.TryGetValue(Norm(instanceId), out var map) && map.TryRemove(conversationKey ?? string.Empty, out _))
        {
            ScheduleSave();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// True when a currently-awaiting chat should be hidden: it was marked handled and no newer customer
    /// message has arrived, or it is snoozed and the snooze hasn't elapsed. Expired overrides are treated as
    /// absent (and lazily pruned on the next save).
    /// </summary>
    public bool IsSuppressed(string instanceId, string conversationKey, DateTimeOffset lastActivityUtc, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(conversationKey) ||
            !_byInstance.TryGetValue(Norm(instanceId), out var map) ||
            !map.TryGetValue(conversationKey, out var ov))
        {
            return false;
        }

        return ov.Kind switch
        {
            // Handled: still suppressed only while no newer message has arrived (activity ≤ recorded).
            OverrideKind.Handled => ov.HandledForActivityUtc is { } h && lastActivityUtc <= h,
            // Snoozed: suppressed until the snooze time passes.
            OverrideKind.Snoozed => ov.SnoozeUntilUtc is { } s && nowUtc < s,
            _ => false
        };
    }

    private void Set(string instanceId, string conversationKey, Override ov)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(conversationKey))
        {
            return;
        }

        var map = _byInstance.GetOrAdd(Norm(instanceId), _ => new ConcurrentDictionary<string, Override>(StringComparer.OrdinalIgnoreCase));
        map[conversationKey] = ov;
        ScheduleSave();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Norm(string instanceId) => (instanceId ?? string.Empty).Trim();

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

            OverrideStore? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer.DeserializeAsync<OverrideStore>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Awaiting-overrides store is corrupt; resetting: {ex.Message}");
                return;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var (instanceId, chats) in store?.Instances ?? [])
            {
                if (string.IsNullOrWhiteSpace(instanceId) || chats is null)
                {
                    continue;
                }

                var map = new ConcurrentDictionary<string, Override>(StringComparer.OrdinalIgnoreCase);
                foreach (var (conv, dto) in chats)
                {
                    if (string.IsNullOrWhiteSpace(conv) || dto is null)
                    {
                        continue;
                    }

                    // Drop already-expired snoozes on load.
                    if (dto.Kind == OverrideKind.Snoozed && dto.SnoozeUntilUtc is { } s && now >= s)
                    {
                        continue;
                    }

                    map[conv] = new Override(dto.Kind, dto.HandledForActivityUtc, dto.SnoozeUntilUtc);
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
                if (generation == Volatile.Read(ref _saveGeneration))
                {
                    await SaveAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // debounced
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Awaiting-overrides save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var store = new OverrideStore();
            foreach (var (instanceId, map) in _byInstance)
            {
                var chats = new Dictionary<string, OverrideDto>(StringComparer.OrdinalIgnoreCase);
                foreach (var (conv, ov) in map)
                {
                    // Prune expired snoozes at save time.
                    if (ov.Kind == OverrideKind.Snoozed && ov.SnoozeUntilUtc is { } s && now >= s)
                    {
                        map.TryRemove(conv, out _);
                        continue;
                    }

                    chats[conv] = new OverrideDto
                    {
                        Kind = ov.Kind,
                        HandledForActivityUtc = ov.HandledForActivityUtc,
                        SnoozeUntilUtc = ov.SnoozeUntilUtc
                    };
                }

                if (chats.Count > 0)
                {
                    store.Instances[instanceId] = chats;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
            var tempPath = _storePath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
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

    internal enum OverrideKind
    {
        Handled,
        Snoozed
    }

    private readonly record struct Override(OverrideKind Kind, DateTimeOffset? HandledForActivityUtc, DateTimeOffset? SnoozeUntilUtc);

    private sealed class OverrideStore
    {
        public int Version { get; set; } = 1;

        public Dictionary<string, Dictionary<string, OverrideDto>> Instances { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class OverrideDto
    {
        public OverrideKind Kind { get; set; }

        public DateTimeOffset? HandledForActivityUtc { get; set; }

        public DateTimeOffset? SnoozeUntilUtc { get; set; }
    }
}
