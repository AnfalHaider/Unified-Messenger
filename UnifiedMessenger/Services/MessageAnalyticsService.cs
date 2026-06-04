using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class DailyActivityPoint
{
    public required string Label { get; init; }

    public int Sent { get; init; }

    public int Received { get; init; }
}

public sealed class ProfessionalAnalyticsSnapshot
{
    public int SentCount { get; init; }

    public int ReceivedCount { get; init; }

    public string AverageReplyTimeDisplay { get; init; } = "—";

    public int SlaBreaches { get; init; }

    public string ResponseRateDisplay { get; init; } = "—";

    public string PeakHourDisplay { get; init; } = "—";

    public string DailyTrendDisplay { get; init; } = "—";

    public bool HasReplyMetrics { get; init; }

    public bool HasMessageVolume { get; init; }

    public IReadOnlyList<DailyActivityPoint> WeeklyActivity { get; init; } = [];

    public IReadOnlyList<OperationalHighlightItem> Highlights { get; init; } = [];

    public MessageTriageDashboardSnapshot Triage { get; init; } = MessageTriageDashboardSnapshot.Empty;

    /// <summary>Empty when all professional branches are included.</summary>
    public string? FilteredBranchInstanceId { get; init; }

    public IReadOnlyList<string> IncludedInstanceIds { get; init; } = [];
}

public sealed class OperationalHighlightItem
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string InstanceDisplayName { get; init; }

    public string? InstanceId { get; init; }
}

public sealed class MessageAnalyticsService
{
    private const string FileName = "analytics.json";
    private const int MaxReplyLatencies = 500;
    private const int DailyBucketRetentionDays = 30;
    private const int SaveDebounceMilliseconds = 750;

    private static readonly Lazy<MessageAnalyticsService> LazyInstance = new(() => new MessageAnalyticsService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<string, InstanceMessageStats> _stats = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;

    public static MessageAnalyticsService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public MessageAnalyticsService()
    {
        _storePath = Path.Combine(ApplicationPaths.UserDataRoot, FileName);
    }

    internal MessageAnalyticsService(string storePath)
    {
        _storePath = storePath;
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

            if (!File.Exists(_storePath))
            {
                _isLoaded = true;
                return;
            }

            AnalyticsStore? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer
                    .DeserializeAsync<AnalyticsStore>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Analytics file is corrupt; resetting to empty: {ex.Message}");
                BackupCorruptFile();
                _isLoaded = true;
                return;
            }

            if (store?.Instances is null)
            {
                _isLoaded = true;
                return;
            }

            _stats.Clear();
            foreach (var (instanceId, dto) in store.Instances)
            {
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    continue;
                }

                var stats = MapFromDto(dto);
                NormalizeStats(stats);
                _stats[instanceId] = stats;
            }

            RecalculateSlaBreachesCore();
            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAllDataAsync(CancellationToken cancellationToken = default)
    {
        CancelScheduledSave();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stats.Clear();

            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ExportToFileAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var store = BuildStoreSnapshot();
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var stream = File.Create(destinationPath);
        await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportCsvAsync(
        IEnumerable<MessengerInstance> instances,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>
        {
            "InstanceId,DisplayName,Platform,Sent,Received,SlaBreaches,AvgReplyMinutes"
        };

        foreach (var instance in instances)
        {
            var sent = GetSentCount(instance.Id);
            var received = GetReceivedCount(instance.Id);
            var sla = GetSlaBreachCount(instance.Id);
            var (totalReply, replyCount, _, _) = GetReplyStats(instance.Id);
            var avgReply = replyCount > 0 ? totalReply / replyCount : 0;

            lines.Add(string.Join(',',
                CsvEscape(instance.Id),
                CsvEscape(instance.DisplayName),
                CsvEscape(instance.Platform),
                sent.ToString(CultureInfo.InvariantCulture),
                received.ToString(CultureInfo.InvariantCulture),
                sla.ToString(CultureInfo.InvariantCulture),
                avgReply.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await File.WriteAllLinesAsync(destinationPath, lines, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    public void RecordMessageSent(string instanceId, string? chatHint = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_debounceLock)
        {
            var stats = _stats.GetOrAdd(instanceId, _ => new InstanceMessageStats());
            stats.SentCount++;
            IncrementDaily(stats.DailySent, 1);

            if (stats.LastReceivedUtc is { } receivedAt)
            {
                var replyAt = DateTimeOffset.UtcNow;
                var deltaMinutes = (replyAt - receivedAt).TotalMinutes;
                if (deltaMinutes >= 0)
                {
                    stats.TotalReplyMinutes += deltaMinutes;
                    stats.ReplyCount++;
                    stats.ReplyLatenciesMinutes.Add(deltaMinutes);
                    TrimReplyLatencies(stats);
                    stats.SlaBreachCount = CountSlaBreaches(stats);
                }
            }

            stats.LastSentUtc = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(chatHint))
            {
                stats.LastChatHint = chatHint.Trim();
            }
        }

        NotifyChanged();
    }

    public void RecordMessageReceived(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_debounceLock)
        {
            ApplyReceivedIncrement(_stats.GetOrAdd(instanceId, _ => new InstanceMessageStats()), DateTimeOffset.UtcNow);
        }

        NotifyChanged();
    }

    /// <summary>
    /// Records inbound activity discovered during startup backfill (one row per conversation).
    /// May add an SLA latency candidate when the message age exceeds the configured threshold.
    /// </summary>
    public void RecordBackfillInbound(
        string instanceId,
        DateTimeOffset receivedAtUtc,
        int slaThresholdMinutes)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_debounceLock)
        {
            var stats = _stats.GetOrAdd(instanceId, _ => new InstanceMessageStats());
            ApplyReceivedIncrement(stats, receivedAtUtc);

            var ageMinutes = (DateTimeOffset.UtcNow - receivedAtUtc).TotalMinutes;
            if (ageMinutes > slaThresholdMinutes)
            {
                stats.ReplyLatenciesMinutes.Add(ageMinutes);
                TrimReplyLatencies(stats);
                stats.SlaBreachCount = CountSlaBreaches(stats);
            }
        }

        NotifyChanged();
    }

    internal void RecordBackfillSlaCandidateForTests(string instanceId, double latencyMinutes)
    {
        lock (_debounceLock)
        {
            var stats = _stats.GetOrAdd(instanceId, _ => new InstanceMessageStats());
            stats.ReplyLatenciesMinutes.Add(latencyMinutes);
            TrimReplyLatencies(stats);
            stats.SlaBreachCount = CountSlaBreaches(stats);
        }

        NotifyChanged();
    }

    private static void ApplyReceivedIncrement(InstanceMessageStats stats, DateTimeOffset receivedAtUtc)
    {
        stats.ReceivedCount++;
        stats.LastReceivedUtc = receivedAtUtc;
        IncrementDaily(stats.DailyReceived, 1);

        var hour = receivedAtUtc.LocalDateTime.Hour;
        if (stats.HourlyReceived.Length == 24)
        {
            stats.HourlyReceived[hour]++;
        }
    }

    internal void SetReplyLatenciesForTests(string instanceId, params double[] latencies)
    {
        lock (_debounceLock)
        {
            var stats = _stats.GetOrAdd(instanceId, _ => new InstanceMessageStats());
            stats.ReplyLatenciesMinutes = latencies.ToList();
            stats.ReplyCount = latencies.Length;
            stats.SlaBreachCount = CountSlaBreaches(stats);
        }
    }

    public void RecalculateSlaBreaches()
    {
        lock (_debounceLock)
        {
            RecalculateSlaBreachesCore();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    public int GetSentCount(string instanceId) =>
        _stats.TryGetValue(instanceId, out var stats) ? stats.SentCount : 0;

    public int GetReceivedCount(string instanceId) =>
        _stats.TryGetValue(instanceId, out var stats) ? stats.ReceivedCount : 0;

    public int GetSlaBreachCount(string instanceId) =>
        _stats.TryGetValue(instanceId, out var stats) ? CountSlaBreaches(stats) : 0;

    public (double TotalReplyMinutes, int ReplyCount, DateTimeOffset? LastReceivedUtc, DateTimeOffset? LastSentUtc)
        GetReplyStats(string instanceId)
    {
        if (!_stats.TryGetValue(instanceId, out var stats))
        {
            return (0, 0, null, null);
        }

        return (stats.TotalReplyMinutes, stats.ReplyCount, stats.LastReceivedUtc, stats.LastSentUtc);
    }

    public ProfessionalAnalyticsSnapshot CaptureProfessionalSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        NotificationHub notificationHub,
        string? branchInstanceId = null)
    {
        var instances = DashboardPageHelper
            .FilterProfessionalInstances(professionalInstances, branchInstanceId)
            .ToList();
        var alertsByInstance = notificationHub.GetAlertsSortedByInstance()
            .GroupBy(a => a.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var sent = 0;
        var received = 0;
        var slaBreaches = 0;
        var totalReplyMinutes = 0.0;
        var replyCount = 0;
        var hourlyTotals = new int[24];

        foreach (var instance in instances)
        {
            sent += GetSentCount(instance.Id);

            var instanceReceived = GetReceivedCount(instance.Id);
            if (instanceReceived == 0 &&
                alertsByInstance.TryGetValue(instance.Id, out var alertCount))
            {
                instanceReceived = alertCount;
            }

            received += instanceReceived;
            slaBreaches += GetSlaBreachCount(instance.Id);

            if (_stats.TryGetValue(instance.Id, out var stats))
            {
                totalReplyMinutes += stats.TotalReplyMinutes;
                replyCount += stats.ReplyCount;

                for (var h = 0; h < 24; h++)
                {
                    if (stats.HourlyReceived.Length == 24)
                    {
                        hourlyTotals[h] += stats.HourlyReceived[h];
                    }
                }
            }
        }

        var weeklyActivity = BuildWeeklyActivity(instances);
        var hasMessageVolume = sent > 0 ||
                               received > 0 ||
                               weeklyActivity.Any(point => point.Sent > 0 || point.Received > 0);
        var hasReplyMetrics = replyCount > 0;

        return new ProfessionalAnalyticsSnapshot
        {
            SentCount = sent,
            ReceivedCount = received,
            AverageReplyTimeDisplay = FormatAverageReplyTime(totalReplyMinutes, replyCount),
            SlaBreaches = slaBreaches,
            ResponseRateDisplay = FormatResponseRate(replyCount, slaBreaches),
            PeakHourDisplay = FormatPeakHour(hourlyTotals),
            DailyTrendDisplay = ComputeDailyTrend(instances),
            HasReplyMetrics = hasReplyMetrics,
            HasMessageVolume = hasMessageVolume,
            WeeklyActivity = weeklyActivity,
            Highlights = BuildOperationalHighlights(instances),
            Triage = MessageTriageService.Instance.BuildSnapshot(instances),
            FilteredBranchInstanceId = string.IsNullOrWhiteSpace(branchInstanceId)
                ? null
                : branchInstanceId.Trim(),
            IncludedInstanceIds = instances.Select(instance => instance.Id).ToList()
        };
    }

    public void NotifyDashboardRefresh()
    {
        NotifyChanged();
    }

    private static double GetSlaThresholdMinutes()
    {
        return Math.Clamp(
            AppSettingsService.Instance.Settings.SlaThresholdMinutes,
            AppSettings.MinSlaThresholdMinutes,
            AppSettings.MaxSlaThresholdMinutes);
    }

    private int CountSlaBreaches(InstanceMessageStats stats)
    {
        if (stats.ReplyLatenciesMinutes.Count > 0)
        {
            var threshold = GetSlaThresholdMinutes();
            return stats.ReplyLatenciesMinutes.Count(minutes => minutes > threshold);
        }

        return stats.SlaBreachCount;
    }

    private void RecalculateSlaBreachesCore()
    {
        var threshold = GetSlaThresholdMinutes();
        foreach (var stats in _stats.Values)
        {
            if (stats.ReplyLatenciesMinutes.Count > 0)
            {
                stats.SlaBreachCount = stats.ReplyLatenciesMinutes.Count(minutes => minutes > threshold);
            }
        }
    }

    private static void IncrementDaily(Dictionary<string, int> buckets, int amount)
    {
        var key = DateTime.Now.ToString("yyyy-MM-dd");
        buckets.TryGetValue(key, out var current);
        buckets[key] = current + amount;
    }

    private static void TrimReplyLatencies(InstanceMessageStats stats)
    {
        if (stats.ReplyLatenciesMinutes.Count <= MaxReplyLatencies)
        {
            return;
        }

        var overflow = stats.ReplyLatenciesMinutes.Count - MaxReplyLatencies;
        stats.ReplyLatenciesMinutes.RemoveRange(0, overflow);
    }

    private static void PruneDailyBuckets(Dictionary<string, int> buckets)
    {
        var cutoff = DateTime.Now.Date.AddDays(-DailyBucketRetentionDays);
        var staleKeys = buckets.Keys
            .Where(key => DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                          && date < cutoff)
            .ToList();

        foreach (var key in staleKeys)
        {
            buckets.Remove(key);
        }
    }

    private static void NormalizeStats(InstanceMessageStats stats)
    {
        stats.HourlyReceived = stats.HourlyReceived.Length == 24
            ? stats.HourlyReceived
            : new int[24];

        PruneDailyBuckets(stats.DailySent);
        PruneDailyBuckets(stats.DailyReceived);
    }

    private static string FormatAverageReplyTime(double totalMinutes, int replyCount)
    {
        if (replyCount == 0)
        {
            return "—";
        }

        var averageMinutes = totalMinutes / replyCount;
        return averageMinutes < 1
            ? "< 1 min"
            : $"{Math.Round(averageMinutes, 0)} min";
    }

    private static string FormatResponseRate(int replyCount, int slaBreaches)
    {
        if (replyCount == 0)
        {
            return "—";
        }

        var withinSla = Math.Max(0, replyCount - slaBreaches);
        var rate = withinSla * 100.0 / replyCount;
        return $"{Math.Round(rate, 0)}%";
    }

    private static string FormatPeakHour(int[] hourlyTotals)
    {
        if (hourlyTotals.All(c => c == 0))
        {
            return "—";
        }

        var peakHour = Array.IndexOf(hourlyTotals, hourlyTotals.Max());
        var time = DateTime.Today.AddHours(peakHour);
        return time.ToString("h tt");
    }

    private IReadOnlyList<DailyActivityPoint> BuildWeeklyActivity(IReadOnlyList<MessengerInstance> instances)
    {
        var sentByDay = new Dictionary<string, int>(StringComparer.Ordinal);
        var receivedByDay = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var instance in instances)
        {
            if (!_stats.TryGetValue(instance.Id, out var stats))
            {
                continue;
            }

            MergeDailyBuckets(sentByDay, stats.DailySent);
            MergeDailyBuckets(receivedByDay, stats.DailyReceived);
        }

        var points = new List<DailyActivityPoint>();
        for (var offset = 6; offset >= 0; offset--)
        {
            var date = DateTime.Now.Date.AddDays(-offset);
            var key = date.ToString("yyyy-MM-dd");
            sentByDay.TryGetValue(key, out var sent);
            receivedByDay.TryGetValue(key, out var received);

            points.Add(new DailyActivityPoint
            {
                Label = offset == 0 ? "Today" : date.ToString("ddd"),
                Sent = sent,
                Received = received
            });
        }

        return points;
    }

    private static void MergeDailyBuckets(Dictionary<string, int> target, Dictionary<string, int> source)
    {
        foreach (var (key, value) in source)
        {
            target.TryGetValue(key, out var current);
            target[key] = current + value;
        }
    }

    private string ComputeDailyTrend(IReadOnlyList<MessengerInstance> instances)
    {
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        var yesterdayKey = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        var today = 0;
        var yesterday = 0;

        foreach (var instance in instances)
        {
            if (!_stats.TryGetValue(instance.Id, out var stats))
            {
                continue;
            }

            stats.DailyReceived.TryGetValue(todayKey, out var todayCount);
            stats.DailyReceived.TryGetValue(yesterdayKey, out var yesterdayCount);
            today += todayCount;
            yesterday += yesterdayCount;
        }

        if (today == 0 && yesterday == 0)
        {
            return "—";
        }

        if (yesterday == 0)
        {
            return $"+{today} vs yesterday";
        }

        var change = (today - yesterday) * 100.0 / yesterday;
        var sign = change >= 0 ? "+" : "";
        return $"{sign}{Math.Round(change, 0)}% vs yesterday";
    }

    private IReadOnlyList<OperationalHighlightItem> BuildOperationalHighlights(
        IReadOnlyList<MessengerInstance> instances)
    {
        var highlights = new List<(DateTimeOffset? SentAt, OperationalHighlightItem Item)>();

        foreach (var instance in instances)
        {
            if (!_stats.TryGetValue(instance.Id, out var stats) ||
                string.IsNullOrWhiteSpace(stats.LastChatHint))
            {
                continue;
            }

            highlights.Add((stats.LastSentUtc, new OperationalHighlightItem
            {
                Title = stats.LastChatHint,
                Subtitle = "Recent outbound thread",
                InstanceDisplayName = instance.DisplayName,
                InstanceId = instance.Id
            }));
        }

        return highlights
            .OrderByDescending(h => h.SentAt ?? DateTimeOffset.MinValue)
            .Select(h => h.Item)
            .Take(8)
            .ToList();
    }

    private void NotifyChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    private void CancelScheduledSave()
    {
        lock (_debounceLock)
        {
            Interlocked.Increment(ref _saveGeneration);
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = null;
        }
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
                // debounced or cleared
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Analytics save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalyticsStore store;
            lock (_debounceLock)
            {
                store = BuildStoreSnapshot();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

            var tempPath = _storePath + ".tmp";
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             options: FileOptions.Asynchronous))
            {
                await JsonSerializer
                    .SerializeAsync(stream, store, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _storePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private AnalyticsStore BuildStoreSnapshot()
    {
        return new AnalyticsStore
        {
            Version = AnalyticsStore.CurrentVersion,
            Instances = _stats.ToDictionary(
                pair => pair.Key,
                pair => MapToDto(pair.Value),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static InstanceMessageStats MapFromDto(InstanceMessageStatsDto dto)
    {
        return new InstanceMessageStats
        {
            SentCount = dto.SentCount,
            ReceivedCount = dto.ReceivedCount,
            SlaBreachCount = dto.SlaBreachCount,
            TotalReplyMinutes = dto.TotalReplyMinutes,
            ReplyCount = dto.ReplyCount,
            LastSentUtc = dto.LastSentUtc,
            LastReceivedUtc = dto.LastReceivedUtc,
            LastChatHint = dto.LastChatHint,
            DailySent = dto.DailySent ?? new Dictionary<string, int>(StringComparer.Ordinal),
            DailyReceived = dto.DailyReceived ?? new Dictionary<string, int>(StringComparer.Ordinal),
            HourlyReceived = dto.HourlyReceived ?? new int[24],
            ReplyLatenciesMinutes = dto.ReplyLatenciesMinutes ?? []
        };
    }

    private static InstanceMessageStatsDto MapToDto(InstanceMessageStats stats)
    {
        NormalizeStats(stats);

        return new InstanceMessageStatsDto
        {
            SentCount = stats.SentCount,
            ReceivedCount = stats.ReceivedCount,
            SlaBreachCount = stats.SlaBreachCount,
            TotalReplyMinutes = stats.TotalReplyMinutes,
            ReplyCount = stats.ReplyCount,
            LastSentUtc = stats.LastSentUtc,
            LastReceivedUtc = stats.LastReceivedUtc,
            LastChatHint = stats.LastChatHint,
            DailySent = stats.DailySent,
            DailyReceived = stats.DailyReceived,
            HourlyReceived = stats.HourlyReceived,
            ReplyLatenciesMinutes = stats.ReplyLatenciesMinutes
        };
    }

    private void BackupCorruptFile()
    {
        try
        {
            if (!File.Exists(_storePath))
            {
                return;
            }

            var backupPath = $"{_storePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(_storePath, backupPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not back up corrupt analytics file: {ex.Message}");
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private sealed class InstanceMessageStats
    {
        public int SentCount { get; set; }

        public int ReceivedCount { get; set; }

        public int SlaBreachCount { get; set; }

        public double TotalReplyMinutes { get; set; }

        public int ReplyCount { get; set; }

        public DateTimeOffset? LastSentUtc { get; set; }

        public DateTimeOffset? LastReceivedUtc { get; set; }

        public string? LastChatHint { get; set; }

        public Dictionary<string, int> DailySent { get; set; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> DailyReceived { get; set; } = new(StringComparer.Ordinal);

        public int[] HourlyReceived { get; set; } = new int[24];

        public List<double> ReplyLatenciesMinutes { get; set; } = [];
    }

    private sealed class AnalyticsStore
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public Dictionary<string, InstanceMessageStatsDto> Instances { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class InstanceMessageStatsDto
    {
        public int SentCount { get; set; }

        public int ReceivedCount { get; set; }

        public int SlaBreachCount { get; set; }

        public double TotalReplyMinutes { get; set; }

        public int ReplyCount { get; set; }

        public DateTimeOffset? LastSentUtc { get; set; }

        public DateTimeOffset? LastReceivedUtc { get; set; }

        public string? LastChatHint { get; set; }

        public Dictionary<string, int>? DailySent { get; set; }

        public Dictionary<string, int>? DailyReceived { get; set; }

        public int[]? HourlyReceived { get; set; }

        public List<double>? ReplyLatenciesMinutes { get; set; }
    }
}
