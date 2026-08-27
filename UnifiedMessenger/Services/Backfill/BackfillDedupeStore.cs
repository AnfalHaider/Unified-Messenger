using System.Collections.Concurrent;
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

    public static string BuildDayKey(
        string instanceId,
        string platform,
        string conversationKey,
        DateTimeOffset timestampUtc)
    {
        var day = timestampUtc.ToUniversalTime().ToString("yyyy-MM-dd");
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
        public List<BackfillDedupeEntryDto> Entries { get; set; } = [];
    }

    private sealed class BackfillDedupeEntryDto
    {
        public string Key { get; set; } = string.Empty;

        public DateTimeOffset LastSeenUtc { get; set; }
    }
}
