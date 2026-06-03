using System.Collections.Concurrent;
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

    public IReadOnlyList<DailyActivityPoint> WeeklyActivity { get; init; } = [];

    public IReadOnlyList<OperationalHighlightItem> Highlights { get; init; } = [];
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

    private static readonly Lazy<MessageAnalyticsService> LazyInstance = new(() => new MessageAnalyticsService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<string, InstanceMessageStats> _stats = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _storePath;
    private readonly object _saveGate = new();
    private CancellationTokenSource? _saveDebounceCts;

    public static MessageAnalyticsService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public MessageAnalyticsService()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnifiedMessenger");
        _storePath = Path.Combine(appDataRoot, FileName);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storePath))
        {
            return;
        }

        await using var stream = File.OpenRead(_storePath);
        var store = await JsonSerializer
            .DeserializeAsync<AnalyticsStore>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (store?.Instances is null)
        {
            return;
        }

        foreach (var (instanceId, dto) in store.Instances)
        {
            _stats[instanceId] = new InstanceMessageStats
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
                HourlyReceived = dto.HourlyReceived ?? new int[24]
            };
        }
    }

    public async Task ClearAllDataAsync(CancellationToken cancellationToken = default)
    {
        _stats.Clear();

        if (File.Exists(_storePath))
        {
            File.Delete(_storePath);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask;
    }

    public async Task ExportToFileAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var export = new AnalyticsStore
        {
            Instances = _stats.ToDictionary(
                pair => pair.Key,
                pair => new InstanceMessageStatsDto
                {
                    SentCount = pair.Value.SentCount,
                    ReceivedCount = pair.Value.ReceivedCount,
                    SlaBreachCount = pair.Value.SlaBreachCount,
                    TotalReplyMinutes = pair.Value.TotalReplyMinutes,
                    ReplyCount = pair.Value.ReplyCount,
                    LastSentUtc = pair.Value.LastSentUtc,
                    LastReceivedUtc = pair.Value.LastReceivedUtc,
                    LastChatHint = pair.Value.LastChatHint,
                    DailySent = pair.Value.DailySent,
                    DailyReceived = pair.Value.DailyReceived,
                    HourlyReceived = pair.Value.HourlyReceived
                },
                StringComparer.OrdinalIgnoreCase)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var stream = File.Create(destinationPath);
        await JsonSerializer.SerializeAsync(stream, export, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public void RecordMessageSent(string instanceId, string? chatHint = null)
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
                if (deltaMinutes > GetSlaThresholdMinutes())
                {
                    stats.SlaBreachCount++;
                }
            }
        }

        stats.LastSentUtc = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(chatHint))
        {
            stats.LastChatHint = chatHint.Trim();
        }

        NotifyChanged();
    }

    public void RecordMessageReceived(string instanceId)
    {
        var stats = _stats.GetOrAdd(instanceId, _ => new InstanceMessageStats());
        stats.ReceivedCount++;
        stats.LastReceivedUtc = DateTimeOffset.UtcNow;
        IncrementDaily(stats.DailyReceived, 1);

        var hour = DateTimeOffset.Now.Hour;
        if (stats.HourlyReceived.Length == 24)
        {
            stats.HourlyReceived[hour]++;
        }

        NotifyChanged();
    }

    public int GetSentCount(string instanceId) =>
        _stats.TryGetValue(instanceId, out var stats) ? stats.SentCount : 0;

    public int GetReceivedCount(string instanceId) =>
        _stats.TryGetValue(instanceId, out var stats) ? stats.ReceivedCount : 0;

    public int GetSlaBreachCount(string instanceId) =>
        _stats.TryGetValue(instanceId, out var stats) ? stats.SlaBreachCount : 0;

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
        NotificationHub notificationHub)
    {
        var instances = professionalInstances.ToList();
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

            if (_stats.TryGetValue(instance.Id, out var stats))
            {
                slaBreaches += stats.SlaBreachCount;
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

        return new ProfessionalAnalyticsSnapshot
        {
            SentCount = sent,
            ReceivedCount = received,
            AverageReplyTimeDisplay = FormatAverageReplyTime(totalReplyMinutes, replyCount),
            SlaBreaches = slaBreaches,
            ResponseRateDisplay = FormatResponseRate(replyCount, slaBreaches),
            PeakHourDisplay = FormatPeakHour(hourlyTotals),
            DailyTrendDisplay = ComputeDailyTrend(instances),
            WeeklyActivity = BuildWeeklyActivity(instances),
            Highlights = BuildOperationalHighlights(instances)
        };
    }

    private static double GetSlaThresholdMinutes()
    {
        var minutes = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        return Math.Clamp(minutes, 5, 120);
    }

    private static void IncrementDaily(Dictionary<string, int> buckets, int amount)
    {
        var key = DateTime.Now.ToString("yyyy-MM-dd");
        buckets.TryGetValue(key, out var current);
        buckets[key] = current + amount;
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

    private void ScheduleSave()
    {
        lock (_saveGate)
        {
            _saveDebounceCts?.Cancel();
            _saveDebounceCts = new CancellationTokenSource();
            var token = _saveDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(750, token).ConfigureAwait(false);
                    await SaveAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // debounced
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Analytics save failed: {ex.Message}");
                }
            }, token);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var store = new AnalyticsStore
        {
            Instances = _stats.ToDictionary(
                pair => pair.Key,
                pair => new InstanceMessageStatsDto
                {
                    SentCount = pair.Value.SentCount,
                    ReceivedCount = pair.Value.ReceivedCount,
                    SlaBreachCount = pair.Value.SlaBreachCount,
                    TotalReplyMinutes = pair.Value.TotalReplyMinutes,
                    ReplyCount = pair.Value.ReplyCount,
                    LastSentUtc = pair.Value.LastSentUtc,
                    LastReceivedUtc = pair.Value.LastReceivedUtc,
                    LastChatHint = pair.Value.LastChatHint,
                    DailySent = pair.Value.DailySent,
                    DailyReceived = pair.Value.DailyReceived,
                    HourlyReceived = pair.Value.HourlyReceived
                },
                StringComparer.OrdinalIgnoreCase)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

        await using var stream = File.Create(_storePath);
        await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken).ConfigureAwait(false);
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
    }

    private sealed class AnalyticsStore
    {
        public Dictionary<string, InstanceMessageStatsDto> Instances { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    }
}
