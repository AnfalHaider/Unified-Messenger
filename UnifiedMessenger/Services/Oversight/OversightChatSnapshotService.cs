using System.Collections.Concurrent;
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
        string ContactPhone = "",
        // Null means "this build/snapshot did not record it", which is NOT the same as false. False is a
        // positive statement that the chat has no last message at all — the signal that a message was
        // deleted for everyone or expired under disappearing messages.
        bool? HasLastMessage = null,
        // WhatsApp's message type: 'chat' for text, 'image'/'video'/'ptt'/'audio'/'document'/'sticker' for
        // media. Needed because an uncaptioned photo and a missing message both produce an empty preview,
        // and they need opposite treatment.
        string LastMessageType = "",
        // WhatsApp's own verdict on a call: Missed, Completed, AcceptedElsewhere, Rejected, Ongoing,
        // Failed. Empty means unknown — the IndexedDB fallback cannot read it, and unknown stays counted.
        // Read live rather than guessed: it is NOT in `subtype`, which is undefined on every call entry.
        string LastCallOutcome = "");

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
            // Widened from JsonException for the same reason as the analytics store: this load runs during
            // startup, and an unreadable — not merely malformed — file used to take the whole app down with
            // it rather than costing one cached snapshot.
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                CorruptFileRecovery.Preserve(_storePath, "Oversight.Snapshot", ex);
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

                    // The same two guards the parser applies to a fresh scan must be applied on LOAD, or a
                    // snapshot written by an older build keeps its bad rows across restarts until a
                    // re-scan happens to replace it. Without this, an upgrading install would carry on
                    // counting WhatsApp's own 0@c.us notice account as a waiting customer, and would keep
                    // rendering base64 image payloads where message text belongs.
                    var chats = dto.Chats
                        .Where(c => !ChatEntryParser.IsNonCustomerConversation(c.ConversationKey))
                        .Select(c => new ChatEntry(
                            c.ConversationKey ?? string.Empty,
                            c.CustomerName ?? string.Empty,
                            c.Unread,
                            c.LastActivityUtc,
                            ChatEntryParser.SanitizePreview(c.Preview ?? string.Empty),
                            c.IsAwaiting,
                            c.LastMessageFromMe,
                            c.ContactPhone ?? string.Empty,
                            c.HasLastMessage,
                            c.LastMessageType ?? string.Empty,
                            c.LastCallOutcome ?? string.Empty)).ToList();

                    // The same coverage retraction the scrapers apply, repeated on LOAD. A snapshot
                    // written by a cold scan carries `hasLastMessage: false` on nearly every row, and
                    // honouring that would close the whole queue on launch and keep closing it until a
                    // warm re-scan happened to replace the file. This actually happened on the owner's
                    // machine: 354 real conversations rendered as 5.
                    var withMessage = chats.Count(c => c.HasLastMessage == true);
                    if (chats.Count > 0 && withMessage * 2 <= chats.Count)
                    {
                        chats = chats.Select(c => c with { HasLastMessage = null }).ToList();
                    }
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
    // override that self-expires when a newer message arrives or the snooze lapses) — or unless the
    // customer's last message plainly ended the conversation.
    //
    // This one predicate is where every awaiting number in the product comes from: the rollup, the
    // digest, the per-account cards and the alert monitor all reach it through TryGetWindowed or
    // BuildDigest. Classifying here rather than at each call site is what stops the headline count and
    // the list underneath it disagreeing.
    private static bool IsEffectivelyAwaiting(string instanceId, ChatEntry chat, DateTimeOffset nowUtc) =>
        chat.IsAwaiting &&
        !IsAutomaticallyClosed(chat) &&
        !AwaitingOverrideStore.Instance.IsSuppressed(instanceId, chat.ConversationKey, chat.LastActivityUtc, nowUtc);

    /// <summary>
    /// Whether the reply-need classifier says this conversation is finished. Always false when the owner
    /// has switched the filter off, so the setting genuinely restores the old raw number.
    /// </summary>
    public static bool IsAutomaticallyClosed(ChatEntry chat) =>
        AppSettingsService.Instance.Settings.FilterClosedConversations &&
        !ClassifyReplyNeed(chat).NeedsReply;

    /// <summary>
    /// Why this chat is or is not being counted: the word rules first, then the local model's cached
    /// answer for the cases the rules called <see cref="ReplyNeedReason.Substantive"/> — which is the
    /// honest name for "unrecognised", not for "definitely needs a reply".
    /// </summary>
    /// <remarks>
    /// The model can only ever move a chat from counted to closed, never the other way. It is read from
    /// cache and never awaited, so a slow or absent Ollama costs nothing and simply leaves the chat
    /// counted — see <see cref="ReplyNeedAdjudicator"/>.
    /// </remarks>
    public static ReplyNeedVerdict ClassifyReplyNeed(ChatEntry chat) =>
        ClassifyReplyNeed(chat, DateTimeOffset.UtcNow);

    internal static ReplyNeedVerdict ClassifyReplyNeed(ChatEntry chat, DateTimeOffset nowUtc)
    {
        var verdict = ReplyNeed.Classify(
            chat.Preview,
            chat.HasLastMessage,
            chat.LastMessageType,
            nowUtc - chat.LastActivityUtc,
            chat.LastMessageFromMe,
            chat.LastCallOutcome);
        if (verdict.Reason != ReplyNeedReason.Substantive)
        {
            return verdict;
        }

        return ReplyNeedAdjudicator.Instance.TryGetNeedsReply(chat.Preview) == false
            ? new ReplyNeedVerdict(false, ReplyNeedReason.AiJudgedClosed)
            : verdict;
    }

    /// <summary>
    /// The awaiting population split into the parts the owner needs separately: what is worth acting on
    /// today, what has aged into backlog, and what the app decided on its own not to count.
    /// </summary>
    /// <remarks>
    /// One number could not carry this. "466 waiting" was true and useless; "58 waiting" alone would hide
    /// a three-month backlog. Reporting all four keeps the headline actionable without quietly dropping
    /// anything — <see cref="Unreadable"/> in particular exists so a scrape that failed to read message
    /// bodies cannot masquerade as an empty queue.
    /// </remarks>
    public readonly record struct AwaitingSplit(int NeedsReply, int Backlog, int ClosedAutomatically, int Unreadable)
    {
        /// <summary>Everything still open, however old — the number the raw direction flag used to give.</summary>
        public int TotalOpen => NeedsReply + Backlog;
    }

    /// <summary>Builds the split across a set of instances.</summary>
    public AwaitingSplit BuildAwaitingSplit(
        IEnumerable<string> instanceIds,
        DateTimeOffset? nowUtc = null,
        int? backlogAfterDays = null)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var days = Math.Max(1, backlogAfterDays ?? AppSettingsService.Instance.Settings.AwaitingBacklogAfterDays);
        var cutoff = now.AddDays(-days);

        var needsReply = 0;
        var backlog = 0;
        var closed = 0;
        var unreadable = 0;

        foreach (var rawId in instanceIds)
        {
            if (string.IsNullOrWhiteSpace(rawId) || !_byInstance.TryGetValue(rawId.Trim(), out var snap))
            {
                continue;
            }

            var id = rawId.Trim();
            foreach (var chat in snap.Chats)
            {
                if (!chat.IsAwaiting)
                {
                    continue;
                }

                // A manual mark-handled is the owner's own decision and is not the classifier's business
                // to report on, so it drops out entirely rather than landing in the auto-closed list.
                if (AwaitingOverrideStore.Instance.IsSuppressed(id, chat.ConversationKey, chat.LastActivityUtc, now))
                {
                    continue;
                }

                if (IsAutomaticallyClosed(chat))
                {
                    closed++;
                    continue;
                }

                if (chat.LastActivityUtc < cutoff)
                {
                    backlog++;
                    continue;
                }

                needsReply++;

                // Counted only against the LIVE queue. "We cannot read 1 of the chats you need to deal
                // with today" is actionable; folding a month-old unreadable chat into the same number
                // makes it alarming and tells the owner nothing they can do anything about.
                if (ClassifyReplyNeed(chat).Reason == ReplyNeedReason.NoPreviewAvailable)
                {
                    unreadable++;
                }
            }
        }

        return new AwaitingSplit(needsReply, backlog, closed, unreadable);
    }

    /// <summary>
    /// The chats the classifier excluded, with the reason, so the owner can check its work rather than
    /// having to trust it. Ordered newest first.
    /// </summary>
    public IReadOnlyList<(ChatEntry Chat, ReplyNeedVerdict Verdict)> GetAutomaticallyClosed(
        IEnumerable<string> instanceIds,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var results = new List<(ChatEntry, ReplyNeedVerdict)>();

        foreach (var rawId in instanceIds)
        {
            if (string.IsNullOrWhiteSpace(rawId) || !_byInstance.TryGetValue(rawId.Trim(), out var snap))
            {
                continue;
            }

            var id = rawId.Trim();
            foreach (var chat in snap.Chats)
            {
                if (!chat.IsAwaiting ||
                    AwaitingOverrideStore.Instance.IsSuppressed(id, chat.ConversationKey, chat.LastActivityUtc, now))
                {
                    continue;
                }

                var verdict = ClassifyReplyNeed(chat);
                if (!verdict.NeedsReply)
                {
                    results.Add((chat, verdict));
                }
            }
        }

        return results.OrderByDescending(r => r.Item1.LastActivityUtc).ToList();
    }

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
    /// <summary>Every chat in the account's last snapshot, or empty when there is none.</summary>
    /// <remarks>
    /// Added for the review-request list, which needs the conversations that are <i>not</i> awaiting — the
    /// ones that ended with the customer saying thank you. <see cref="GetAwaiting"/> is the opposite filter,
    /// so it could not serve that.
    /// </remarks>
    public IReadOnlyList<ChatEntry> GetChats(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && _byInstance.TryGetValue(instanceId.Trim(), out var snapshot)
            ? snapshot.Chats
            : [];

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
                AppLogger.LogWarning("Oversight.Snapshot", $"Oversight snapshot save failed: {ex.Message}");
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
                            ContactPhone = c.ContactPhone,
                            HasLastMessage = c.HasLastMessage,
                            LastMessageType = c.LastMessageType,
                            LastCallOutcome = c.LastCallOutcome
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

        // Nullable on purpose: a snapshot written before these existed must round-trip as "unknown", not
        // as "this chat has no message" — which would mass-close an upgrading install's whole queue.
        public bool? HasLastMessage { get; set; }

        public string? LastMessageType { get; set; }

        public string? LastCallOutcome { get; set; }
    }
}
