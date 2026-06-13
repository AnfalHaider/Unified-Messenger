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

            await using var stream = File.OpenRead(_storePath);
            var dto = await JsonSerializer.DeserializeAsync<BackfillDedupeStoreDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
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
            await using var stream = File.Create(_storePath);
            await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken).ConfigureAwait(false);
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
