using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

/// <summary>
/// Persists conversation+day keys so startup backfill does not re-ingest the same thread on the same day.
/// </summary>
public sealed class BackfillDedupeStore
{
    private const string FileName = "backfill_dedupe.json";

    private static readonly Lazy<BackfillDedupeStore> LazyInstance = new(() => new BackfillDedupeStore());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storePath;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isLoaded;

    public BackfillDedupeStore()
    {
        _storePath = Path.Combine(ApplicationPaths.UserDataRoot, FileName);
    }

    internal BackfillDedupeStore(string storePath)
    {
        _storePath = storePath;
    }

    public static BackfillDedupeStore Instance => LazyInstance.Value;

    /// <summary>
    /// The conversation+day key that gates one backfill row per conversation per day.
    /// </summary>
    /// <remarks>
    /// The day is the <b>local</b> calendar day, because that is the day the count lands in: accepting a row
    /// here is what runs <c>MessageAnalyticsService.RecordBackfillInbound</c>, whose daily bucket is keyed by
    /// <see cref="LocalDayBoundary.LocalDate"/>. This used to key by the UTC day, and at the owner's UTC+5
    /// the two boundaries sit five hours apart, so the gate and the bucket disagreed about which day it was
    /// for the first five hours of every local day — in both directions. A conversation active at 02:00 and
    /// again at 20:00 local straddles the UTC boundary, so both rows were accepted and that one local day
    /// was counted twice; a conversation active at 20:00 and again at 02:00 the next morning shares a UTC
    /// day, so the second row was dropped and the new local day recorded nothing for it at all. Neither is
    /// visible anywhere — it just moves the messages-per-day figure and the activity chart a row at a time.
    /// </remarks>
    public static string BuildDayKey(
        string instanceId,
        string platform,
        string conversationKey,
        DateTimeOffset timestampUtc)
    {
        var day = LocalDayBoundary.LocalDate(timestampUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var normalizedPlatform = PlatformDefinition.NormalizePlatformId(platform);
        var conversation = string.IsNullOrWhiteSpace(conversationKey) ? string.Empty : conversationKey.Trim();
        return $"{instanceId.Trim()}|{normalizedPlatform}|{conversation}|{day}";
    }

    public async Task<bool> TryAcceptForDayAsync(
        string instanceId,
        string platform,
        string conversationKey,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var key = BuildDayKey(instanceId, platform, conversationKey, timestampUtc);
        if (_seen.ContainsKey(key))
        {
            return false;
        }

        _seen[key] = timestampUtc.ToUniversalTime();
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _seen.Clear();
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void ResetForTests()
    {
        _seen.Clear();
        _isLoaded = true;
        if (File.Exists(_storePath))
        {
            File.Delete(_storePath);
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

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

            BackfillDedupeStoreDto? dto;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                dto = await JsonSerializer.DeserializeAsync<BackfillDedupeStoreDto>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            // This load had no handler of any kind, so an unreadable file threw out of every caller of
            // EnsureLoadedAsync. Starting from an empty dedupe set costs a re-ingest of already-seen
            // messages, which the analytics store deduplicates again by key — it does not cost correctness.
            // Failing the backfill outright did.
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                CorruptFileRecovery.Preserve(_storePath, "Backfill.Dedupe", ex);
                _isLoaded = true;
                return;
            }

            if (dto is not null && dto.Version != BackfillDedupeStoreDto.CurrentVersion)
            {
                // Keys from an older shape can never match one built now, so keeping them would only grow
                // the file. Dropping them costs at most one re-ingested row per conversation, once.
                AppLogger.LogInfo(
                    "Backfill.Dedupe",
                    $"Discarding {dto.Entries?.Count ?? 0} dedupe key(s) written in format v{dto.Version}; "
                    + $"keys are now built on the local calendar day (v{BackfillDedupeStoreDto.CurrentVersion}).");
                _isLoaded = true;
                return;
            }

            if (dto?.Entries is not null)
            {
                foreach (var entry in dto.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    _seen[entry.Key] = entry.LastSeenUtc;
                }
            }

            PruneStaleEntries();
            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PruneStaleEntries();
            var dto = new BackfillDedupeStoreDto
            {
                Version = BackfillDedupeStoreDto.CurrentVersion,
                Entries = _seen
                    .Select(pair => new BackfillDedupeEntryDto
                    {
                        Key = pair.Key,
                        LastSeenUtc = pair.Value
                    })
                    .ToList()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

            // Was a bare File.Create over the live path, which truncates before it writes: a crash or a
            // full disk mid-serialize left a half-written store that the next load could not parse. Every
            // other store in the app writes to a temp file and moves it into place; this one did not.
            var tempPath = _storePath + ".tmp";
            await using (var stream = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _storePath, overwrite: true);
        }
        // This save runs once per accepted conversation inside the backfill loop, so an unwritable file
        // used to abort the entire backfill for that account partway through — and say nothing anywhere.
        // A missed dedupe write costs a re-ingest of one day's conversation, which the analytics store
        // deduplicates again by key. Throttled because the call site is per-conversation.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.LogWarningThrottled(
                "Backfill.Dedupe",
                $"Could not record backfill dedupe state: {ex.GetType().Name}",
                "backfill-dedupe-save");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void PruneStaleEntries()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-45);
        foreach (var pair in _seen)
        {
            if (pair.Value < cutoff)
            {
                _seen.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class BackfillDedupeStoreDto
    {
        /// <summary>
        /// Bumped when the shape of a key changes, so entries written under the old shape are discarded
        /// rather than sitting in the map never matching anything. Version 2 moved the day component from
        /// the UTC calendar day to the local one — see <see cref="BuildDayKey"/>.
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// Defaults to 1, not <see cref="CurrentVersion"/>. A file written before this field existed has no
        /// <c>version</c> property at all, and System.Text.Json leaves an absent property at whatever the
        /// initializer set — so defaulting to the current version would quietly declare every legacy file
        /// up to date and keep exactly the keys this version exists to discard. Writers set it explicitly.
        /// </summary>
        public int Version { get; set; } = 1;

        public List<BackfillDedupeEntryDto> Entries { get; set; } = [];
    }

    private sealed class BackfillDedupeEntryDto
    {
        public string Key { get; set; } = string.Empty;

        public DateTimeOffset LastSeenUtc { get; set; }
    }
}
