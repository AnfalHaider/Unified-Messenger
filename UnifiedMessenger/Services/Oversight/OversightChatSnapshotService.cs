using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Holds the latest unread-based oversight data per instance, read directly from WhatsApp Web's chat
/// store: for each active chat, its unread count and last-activity time. This is WhatsApp's own "needs
/// attention" signal — reliable for every chat, no message history or name matching needed — and is the
/// command center's primary on-time source. Storing per-chat last-activity lets the date window scope
/// the metric: "of the chats active in the window, how many are caught up (no unread)".
/// </summary>
public sealed class OversightChatSnapshotService
{
    public readonly record struct ChatEntry(
        string ConversationKey,
        string CustomerName,
        int Unread,
        DateTimeOffset LastActivityUtc,
        string Preview = "",
        bool IsAwaiting = false,
        bool LastMessageFromMe = false,
        string ContactPhone = "");

    /// <summary>"Since you were last here" summary across a set of instances.</summary>
    public readonly record struct OversightDigest(
        int NewAwaiting,
        int TotalAwaiting,
        int AccountsWithAwaiting,
        DateTimeOffset? OldestActivityUtc,
        bool HasData);

    private sealed record InstanceChats(IReadOnlyList<ChatEntry> Chats, DateTimeOffset CapturedAtUtc);

    private const string FileName = "oversight-snapshot.json";
    private const int SaveDebounceMilliseconds = 750;

    private static readonly Lazy<OversightChatSnapshotService> LazyInstance = new(() => new OversightChatSnapshotService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static OversightChatSnapshotService Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<string, InstanceChats> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;

    private OversightChatSnapshotService()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal OversightChatSnapshotService(string storePath)
    {
        _storePath = storePath;
    }

    /// <summary>The most recent capture time across all instances — the "as of" stamp the dashboard shows.</summary>
    public DateTimeOffset? LastCapturedUtc =>
        _byInstance.IsEmpty ? null : _byInstance.Values.Max(v => v.CapturedAtUtc);

    /// <summary>When this instance's chats were last captured, or null if it has no snapshot yet.
    /// Feeds the per-card "Updated Xm ago" freshness line so stale data is visible per account.</summary>
    public DateTimeOffset? TryGetCapturedAtUtc(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && _byInstance.TryGetValue(instanceId.Trim(), out var snap)
            ? snap.CapturedAtUtc
            : null;

    public void Update(string instanceId, IReadOnlyList<ChatEntry> chats, DateTimeOffset capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || chats is null)
        {
            return;
        }

        var key = instanceId.Trim();
        var resolved = ApplyStickyAwaiting(key, chats);
        _byInstance[key] = new InstanceChats(resolved, capturedAtUtc);

        // Feed the response-time tracker the post-sticky state so it measures First Response Time from real
        // message timestamps as chats move awaiting → replied across syncs.
        foreach (var chat in resolved)
        {
            ResponseTimeTracker.Instance.Observe(
                key, chat.ConversationKey, chat.IsAwaiting, chat.LastMessageFromMe, chat.LastActivityUtc);

            // Track first/last-seen per customer for the new-vs-returning insight (groups are filtered inside).
            ContactHistoryStore.Instance.Observe(
                key, chat.ConversationKey, chat.ContactPhone, chat.LastActivityUtc);
        }

        ScheduleSave();
    }

    /// <summary>
    /// Loads the last-persisted oversight snapshot so the command center shows last-known numbers
    /// immediately on launch (labeled "as of …"), instead of going blank until the next scan. Idempotent;
    /// a fresh scan via <see cref="Update"/> replaces an instance's chats with the latest truth.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            if (!File.Exists(_storePath))
            {
                _isLoaded = true;
                return;
            }

            SnapshotStore? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer
                    .DeserializeAsync<SnapshotStore>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Oversight snapshot is corrupt; resetting: {ex.Message}");
                BackupCorruptFile();
                _isLoaded = true;
                return;
            }

            if (store?.Instances is not null)
            {
                foreach (var (instanceId, dto) in store.Instances)
                {
                    if (string.IsNullOrWhiteSpace(instanceId) || dto.Chats is null)
                    {
                        continue;
                    }

                    var chats = dto.Chats.Select(c => new ChatEntry(
                        c.ConversationKey ?? string.Empty,
                        c.CustomerName ?? string.Empty,
                        c.Unread,
                        c.LastActivityUtc,
                        c.Preview ?? string.Empty,
                        c.IsAwaiting,
                        c.LastMessageFromMe,
                        c.ContactPhone ?? string.Empty)).ToList();
                    _byInstance[instanceId] = new InstanceChats(chats, dto.CapturedAtUtc);
                }
            }

            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Safety valve for sticky-awaiting: a chat can only be carried as "awaiting" via inheritance while its
    /// last activity is this recent. Past it, an unconfirmed-direction read is allowed to clear it, so a chat
    /// whose outbound reply we never observed (no DOM hint, no persisted lastMessage) can't stay stuck on the
    /// list forever. A genuinely-still-awaiting chat keeps getting fresh awaiting=true reads and is unaffected.
    /// </summary>
    private static readonly TimeSpan StickyAwaitingMaxAge = TimeSpan.FromDays(7);

    /// <summary>
    /// Keeps a chat marked "awaiting" until we actually observe an outbound reply — opening/reading a chat
    /// (which clears WhatsApp's unread marker) must NOT count as responding. A chat stays awaiting unless
    /// the new read confirms the last message is now from us (<see cref="ChatEntry.LastMessageFromMe"/>);
    /// an "awaiting=false" with unconfirmed direction inherits the prior awaiting state so an opened-but-
    /// unanswered chat doesn't silently flip to "caught up" — but only up to <see cref="StickyAwaitingMaxAge"/>
    /// so it can't get permanently stuck.
    /// </summary>
    private IReadOnlyList<ChatEntry> ApplyStickyAwaiting(string key, IReadOnlyList<ChatEntry> incoming)
    {
        var prior = _byInstance.TryGetValue(key, out var snap)
            ? snap.Chats.ToDictionary(c => c.ConversationKey, c => c, StringComparer.OrdinalIgnoreCase)
            : null;

        var nowUtc = DateTimeOffset.UtcNow;
        var result = new List<ChatEntry>(incoming.Count);
        foreach (var chat in incoming)
        {
            if (chat.IsAwaiting || chat.LastMessageFromMe)
            {
                // Trust the read: still awaiting, or confirmed replied (last message is from us).
                result.Add(chat);
                continue;
            }

            // awaiting=false but the read did NOT confirm an outbound reply (direction unknown, e.g. the chat
            // was opened off-screen so unread dropped to 0). Inherit the prior awaiting state if we had one —
            // but only while the chat is still recent, so it can't stick indefinitely (safety valve).
            var stillAwaiting = prior is not null &&
                                prior.TryGetValue(chat.ConversationKey, out var was) &&
                                was.IsAwaiting &&
                                (nowUtc - chat.LastActivityUtc) <= StickyAwaitingMaxAge;
            result.Add(stillAwaiting ? chat with { IsAwaiting = true } : chat);
        }

        return result;
    }

    /// <summary>
    /// Active = chats caught up within the window PLUS every chat currently awaiting a reply; CaughtUp =
    /// the in-window chats with no customer waiting. A chat awaiting a reply is <b>current state</b> and is
    /// always counted (and never as "caught up") regardless of the date window — a customer who has been
    /// waiting since yesterday still needs a reply today, so it must not drop out of "Today". The window
    /// still scopes the caught-up chats, so the on-time % reflects recent handling. Returns false when there
    /// is no snapshot for the instance.
    /// </summary>
    public bool TryGetWindowed(
        string instanceId,
        DateTimeOffset? windowStartUtc,
        out int active,
        out int caughtUp,
        DateTimeOffset? windowEndUtc = null)
    {
        active = 0;
        caughtUp = 0;
        if (string.IsNullOrWhiteSpace(instanceId) || !_byInstance.TryGetValue(instanceId.Trim(), out var snap))
        {
            return false;
        }

        var id = instanceId.Trim();
        var now = DateTimeOffset.UtcNow;
        foreach (var chat in snap.Chats)
        {
            if (IsEffectivelyAwaiting(id, chat, now))
            {
                // Current-state backlog — always counts, never "caught up", independent of the window.
                active++;
                continue;
            }

            if (!InWindow(chat.LastActivityUtc, windowStartUtc, windowEndUtc))
            {
                continue;
            }

            active++;
            caughtUp++;
        }

        return true;
    }

    // A chat awaiting a reply unless the owner manually marked it handled-elsewhere or snoozed it (an
    // override that self-expires when a newer message arrives or the snooze lapses).
    private static bool IsEffectivelyAwaiting(string instanceId, ChatEntry chat, DateTimeOffset nowUtc) =>
        chat.IsAwaiting &&
        !AwaitingOverrideStore.Instance.IsSuppressed(instanceId, chat.ConversationKey, chat.LastActivityUtc, nowUtc);

    /// <summary>
    /// Summarize awaiting state across instances for the "since you were last here" digest: how many are
    /// awaiting in total, how many arrived since <paramref name="sinceUtc"/>, across how many accounts, and
    /// the oldest waiting activity. <c>HasData</c> is false until at least one instance has a snapshot.
    /// </summary>
    public OversightDigest BuildDigest(IEnumerable<string> instanceIds, DateTimeOffset? sinceUtc)
    {
        var total = 0;
        var fresh = 0;
        var accounts = 0;
        var hasData = false;
        DateTimeOffset? oldest = null;

        foreach (var id in instanceIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(id) || !_byInstance.TryGetValue(id.Trim(), out var snap))
            {
                continue;
            }

            hasData = true;
            var awaitingHere = 0;
            var idTrim = id.Trim();
            var nowUtc = DateTimeOffset.UtcNow;
            foreach (var chat in snap.Chats)
            {
                if (!IsEffectivelyAwaiting(idTrim, chat, nowUtc))
                {
                    continue;
                }

                awaitingHere++;
                total++;
                if (sinceUtc is null || chat.LastActivityUtc > sinceUtc.Value)
                {
                    fresh++;
                }
                if (oldest is null || chat.LastActivityUtc < oldest.Value)
                {
                    oldest = chat.LastActivityUtc;
                }
            }

            if (awaitingHere > 0)
            {
                accounts++;
            }
        }

        return new OversightDigest(fresh, total, accounts, oldest, hasData);
    }

    private static bool InWindow(DateTimeOffset when, DateTimeOffset? startUtc, DateTimeOffset? endUtc) =>
        (startUtc is null || when >= startUtc.Value) &&
        (endUtc is null || when <= endUtc.Value);

    /// <summary>
    /// Every chat currently awaiting a reply, worst-first (most unread, then most recent). Awaiting is
    /// <b>current state</b>, so the date-window parameters are intentionally ignored — a customer waiting
    /// since last week must still appear in "Today". Kept as parameters for call-site compatibility. Empty
    /// when there is no snapshot.
    /// </summary>
    public IReadOnlyList<ChatEntry> GetAwaiting(
        string instanceId,
        DateTimeOffset? windowStartUtc = null,
        DateTimeOffset? windowEndUtc = null)
    {
        _ = windowStartUtc;
        _ = windowEndUtc;
        if (string.IsNullOrWhiteSpace(instanceId) || !_byInstance.TryGetValue(instanceId.Trim(), out var snap))
        {
            return [];
        }

        var id = instanceId.Trim();
        var now = DateTimeOffset.UtcNow;
        return snap.Chats
            .Where(c => IsEffectivelyAwaiting(id, c, now))
            .OrderByDescending(c => c.Unread)
            .ThenByDescending(c => c.LastActivityUtc)
            .ToList();
    }

    /// <summary>Forces any pending debounced save to disk (call on app suspend/exit).</summary>
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
                Debug.WriteLine($"Oversight snapshot save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = new SnapshotStore
            {
                Version = SnapshotStore.CurrentVersion,
                Instances = _byInstance.ToDictionary(
                    pair => pair.Key,
                    pair => new InstanceSnapshotDto
                    {
                        CapturedAtUtc = pair.Value.CapturedAtUtc,
                        Chats = pair.Value.Chats.Select(c => new ChatEntryDto
                        {
                            ConversationKey = c.ConversationKey,
                            CustomerName = c.CustomerName,
                            Unread = c.Unread,
                            LastActivityUtc = c.LastActivityUtc,
                            Preview = c.Preview,
                            IsAwaiting = c.IsAwaiting,
                            LastMessageFromMe = c.LastMessageFromMe,
                            ContactPhone = c.ContactPhone
                        }).ToList()
                    },
                    StringComparer.OrdinalIgnoreCase)
            };

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

    private void BackupCorruptFile()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                File.Move(_storePath, $"{_storePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak", overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not back up corrupt oversight snapshot: {ex.Message}");
        }
    }

    private sealed class SnapshotStore
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public Dictionary<string, InstanceSnapshotDto> Instances { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class InstanceSnapshotDto
    {
        public DateTimeOffset CapturedAtUtc { get; set; }

        public List<ChatEntryDto>? Chats { get; set; }
    }

    private sealed class ChatEntryDto
    {
        public string? ConversationKey { get; set; }

        public string? CustomerName { get; set; }

        public int Unread { get; set; }

        public DateTimeOffset LastActivityUtc { get; set; }

        public string? Preview { get; set; }

        public bool IsAwaiting { get; set; }

        public bool LastMessageFromMe { get; set; }

        public string? ContactPhone { get; set; }
    }
}
