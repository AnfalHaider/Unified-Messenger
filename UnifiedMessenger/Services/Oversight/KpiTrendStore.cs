using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// A tiny daily history of the whole-business KPIs that aren't otherwise time-series'd — the caught-up %
/// and the awaiting-reply count — so the dashboard can show a micro-trend sparkline under those tiles. One
/// value per local day (the latest render of the day wins); ~90-day retention; fully local.
/// </summary>
public sealed class KpiTrendStore
{
    private const string FileName = "kpi-trend.json";
    private const int RetentionDays = 90;
    private const int SaveDebounceMilliseconds = 1500;

    private static readonly Lazy<KpiTrendStore> LazyInstance = new(() => new KpiTrendStore());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static KpiTrendStore Instance => LazyInstance.Value;

    // dayKey (yyyy-MM-dd, local) -> point.
    private readonly ConcurrentDictionary<string, DayPoint> _byDay = new(StringComparer.Ordinal);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;

    private KpiTrendStore()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal KpiTrendStore(string storePath)
    {
        _storePath = storePath;
    }

    /// <summary>Record today's whole-business caught-up % and awaiting count (overwrites the day's value).</summary>
    public void Record(int caughtUpPercent, int awaiting)
    {
        var key = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _byDay[key] = new DayPoint(Math.Clamp(caughtUpPercent, 0, 100), Math.Max(0, awaiting));
        ScheduleSave();
    }

    /// <summary>Writes a reading for a specific local day. Tests only — production always records today.</summary>
    internal void RecordForTests(DateTime localDay, int caughtUpPercent, int awaiting)
    {
        var key = localDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _byDay[key] = new DayPoint(Math.Clamp(caughtUpPercent, 0, 100), Math.Max(0, awaiting));
    }

    /// <summary>Caught-up % for the last <paramref name="days"/> days, oldest→newest, stopping at the first gap.</summary>
    public IReadOnlyList<int> GetCaughtUpTrend(int days = 14) => Series(days, p => p.CaughtUpPercent);

    /// <summary>Awaiting count for the last <paramref name="days"/> days, oldest→newest.</summary>
    public IReadOnlyList<int> GetAwaitingTrend(int days = 14) => Series(days, p => p.Awaiting);

    /// <summary>
    /// The caught-up % as a date-keyed map (yyyy-MM-dd → value) over the last <paramref name="days"/> days.
    /// Unlike <see cref="GetCaughtUpTrend"/>, this preserves which day each value belongs to, so
    /// <see cref="ChartSeriesBuilder.BuildZeroPaddedDailySeries"/> can index-align it and slice it into
    /// week-over-week — the omit-empty-days list cannot be sliced reliably.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetCaughtUpByDay(int days = 14) => DateKeyed(days, p => p.CaughtUpPercent);

    /// <summary>The awaiting count as a date-keyed map over the last <paramref name="days"/> days.</summary>
    public IReadOnlyDictionary<string, int> GetAwaitingByDay(int days = 14) => DateKeyed(days, p => p.Awaiting);

    private IReadOnlyDictionary<string, int> DateKeyed(int days, Func<DayPoint, int> select)
    {
        var today = DateTime.Now.Date;
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = days - 1; i >= 0; i--)
        {
            var key = today.AddDays(-i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (_byDay.TryGetValue(key, out var p))
            {
                map[key] = select(p);
            }
        }

        return map;
    }

    /// <summary>
    /// The most recent unbroken run of daily readings, oldest→newest.
    /// </summary>
    /// <remarks>
    /// Days with no reading used to be dropped from the middle of the series, which made a sparkline lie
    /// about time: three readings taken weeks apart plotted as three adjacent points, so a slope the owner
    /// read as "this week" could span a month. The x-axis has no labels, so nothing on screen contradicted
    /// it. Carrying the last value forward across a gap would be worse — inventing readings for days the
    /// app was never opened. So the series stops at the first gap instead: what it draws is real and
    /// evenly spaced, and when there are not two consecutive days <c>MiniSparkline</c> already renders
    /// nothing rather than a trend from one point.
    /// </remarks>
    private IReadOnlyList<int> Series(int days, Func<DayPoint, int> select)
    {
        var today = DateTime.Now.Date;
        var result = new List<int>();

        // Walk backwards from today and stop at the first day with no reading.
        for (var i = 0; i < days; i++)
        {
            var key = today.AddDays(-i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!_byDay.TryGetValue(key, out var p))
            {
                break;
            }

            result.Add(select(p));
        }

        result.Reverse();
        return result;
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

            TrendStore? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer.DeserializeAsync<TrendStore>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                CorruptFileRecovery.Preserve(_storePath, "KpiTrends", ex);
                return;
            }

            var cutoff = DateTime.Now.Date.AddDays(-RetentionDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var (day, dto) in store?.Days ?? [])
            {
                if (!string.IsNullOrWhiteSpace(day) && dto is not null && string.CompareOrdinal(day, cutoff) >= 0)
                {
                    _byDay[day] = new DayPoint(Math.Clamp(dto.CaughtUpPercent, 0, 100), Math.Max(0, dto.Awaiting));
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
                AppLogger.LogWarning("KpiTrends", $"KPI-trend save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-RetentionDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var store = new TrendStore();
            foreach (var (day, p) in _byDay)
            {
                if (string.CompareOrdinal(day, cutoff) >= 0)
                {
                    store.Days[day] = new DayPointDto { CaughtUpPercent = p.CaughtUpPercent, Awaiting = p.Awaiting };
                }
                else
                {
                    _byDay.TryRemove(day, out _);
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

    private readonly record struct DayPoint(int CaughtUpPercent, int Awaiting);

    private sealed class TrendStore
    {
        public int Version { get; set; } = 1;

        public Dictionary<string, DayPointDto> Days { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class DayPointDto
    {
        public int CaughtUpPercent { get; set; }

        public int Awaiting { get; set; }
    }
}
