using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// A daily reading per Google account — rating, lifetime total, and the answered/unanswered split — so the
/// Review Desk can say what changed rather than only what is true this second.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> Nothing about reviews was stored between runs. <c>GoogleReviewSnapshotService</c>
/// holds a <c>ConcurrentDictionary</c> that dies with the process, so every restart began from zero: no
/// trend, no velocity, and a rating that vanished until the six-hourly scrape ran again. Half the tiles in
/// the approved design were unbuildable for that one reason.
/// </para>
/// <para>
/// <b>One reading per local day, last write wins.</b> Reviews move slowly and the background pass runs every
/// 30 minutes; keeping every pass would be ~48 rows a day describing the same state. The last reading of a
/// day is the one that saw the most of it.
/// </para>
/// <para>
/// <b>A failed scrape must not be recorded as a zero.</b> <see cref="Record"/> takes nullable figures and
/// keeps the day's existing value for anything it was not given, because a day stored as "rating 0.0,
/// total 0" would render as a catastrophic collapse rather than as the missed reading it was.
/// </para>
/// </remarks>
public sealed class ReviewHistoryStore
{
    private const string FileName = "review-history.json";

    /// <summary>Just over a year, so a future year-on-year comparison has something to stand on.</summary>
    private const int RetentionDays = 400;

    private const int SaveDebounceMilliseconds = 2000;

    private static readonly Lazy<ReviewHistoryStore> LazyInstance = new(() => new ReviewHistoryStore());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ReviewHistoryStore Instance => LazyInstance.Value;

    // instanceId -> dayKey (yyyy-MM-dd, local) -> reading.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Reading>> _byAccount =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;

    private ReviewHistoryStore()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal ReviewHistoryStore(string storePath) => _storePath = storePath;

    /// <summary>
    /// Records today's reading for one account, merging with anything already stored for today.
    /// </summary>
    /// <remarks>
    /// Nulls mean "not read this time", never "zero". The reviews scrape and the rating scrape run on
    /// different schedules and fail independently, so most days one of them writes without the other.
    /// </remarks>
    public void Record(string instanceId, double? rating, int? lifetimeTotal, int? unanswered, int? answered)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var key = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var days = _byAccount.GetOrAdd(instanceId.Trim(), _ => new ConcurrentDictionary<string, Reading>(StringComparer.Ordinal));

        days.AddOrUpdate(
            key,
            _ => new Reading(rating, lifetimeTotal, unanswered, answered),
            (_, existing) => new Reading(
                rating ?? existing.Rating,
                lifetimeTotal ?? existing.LifetimeTotal,
                unanswered ?? existing.Unanswered,
                answered ?? existing.Answered));

        ScheduleSave();
    }

    /// <summary>Every stored reading for one account, oldest first.</summary>
    public IReadOnlyList<ReviewDayPoint> GetHistory(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            !_byAccount.TryGetValue(instanceId.Trim(), out var days))
        {
            return [];
        }

        var points = new List<ReviewDayPoint>();
        foreach (var (day, reading) in days)
        {
            if (DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                points.Add(new ReviewDayPoint(
                    parsed, reading.Rating, reading.LifetimeTotal,
                    reading.Unanswered ?? 0, reading.Answered ?? 0));
            }
        }

        return points.OrderBy(p => p.Day).ToList();
    }

    /// <summary>
    /// The business-wide history: each day's readings summed across accounts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rating is a mean weighted by each account's lifetime total, matching how the hero computes today's
    /// figure — an unweighted mean would let a 244-review location move the number as much as a 992-review one.
    /// </para>
    /// <para>
    /// <b>A day only counts when every requested account reported.</b> This previously required only that the
    /// accounts <i>present that day</i> agreed, which is not the same thing and produced invented movement:
    /// rating scrapes fail per account routinely, so a day where one of three locations reported gave a total
    /// of 435 against the previous day's 1,671 and that location's own 4.7 against a weighted 4.63. The trend
    /// then read "up 0.1" when nothing had moved, and the velocity tile read "+1,236 new reviews" the moment
    /// the missing two came back. A partial day is not a smaller reading of the business, it is a reading of a
    /// different business, so it is dropped entirely rather than blended in.
    /// </para>
    /// <para>
    /// The cost is that one permanently-failing location suppresses the whole business trend. That is the
    /// right way round: no trend is recoverable, a wrong one is not.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ReviewDayPoint> GetCombinedHistory(IEnumerable<string> instanceIds)
    {
        var ids = instanceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var byDay = new Dictionary<DateOnly, List<ReviewDayPoint>>();
        foreach (var id in ids)
        {
            foreach (var point in GetHistory(id))
            {
                if (!byDay.TryGetValue(point.Day, out var list))
                {
                    byDay[point.Day] = list = [];
                }

                list.Add(point);
            }
        }

        var combined = new List<ReviewDayPoint>();
        foreach (var (day, points) in byDay.OrderBy(kv => kv.Key))
        {
            // Every requested account must have a reading, or the day describes a different set of
            // locations from its neighbours and the difference between them is not a change in the business.
            if (points.Count != ids.Count)
            {
                continue;
            }

            // The lifetime total is all-or-nothing for the same reason. The rating scrape and the reviews
            // scrape fail independently, so a day can legitimately carry counts but no totals.
            var haveEveryTotal = points.All(p => p.LifetimeTotal is > 0);
            int? total = haveEveryTotal ? points.Sum(p => p.LifetimeTotal!.Value) : null;

            double? rating = null;
            if (haveEveryTotal && points.All(p => p.Rating is not null))
            {
                var weight = points.Sum(p => p.LifetimeTotal!.Value);
                if (weight > 0)
                {
                    rating = points.Sum(p => p.Rating!.Value * p.LifetimeTotal!.Value) / weight;
                }
            }

            combined.Add(new ReviewDayPoint(
                day, rating, total,
                points.Sum(p => p.Unanswered),
                points.Sum(p => p.Answered)));
        }

        return combined;
    }

    /// <summary>How many distinct days of readings exist for these accounts.</summary>
    /// <remarks>Drives the "starts building today" wording, so the page can say how far off a trend is.</remarks>
    public int DaysOfHistory(IEnumerable<string> instanceIds) => GetCombinedHistory(instanceIds).Count;

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

            HistoryFile? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer.DeserializeAsync<HistoryFile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                CorruptFileRecovery.Preserve(_storePath, "ReviewHistory", ex);
                return;
            }

            var cutoff = DateTime.Now.Date.AddDays(-RetentionDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var (id, days) in store?.Accounts ?? [])
            {
                if (string.IsNullOrWhiteSpace(id) || days is null)
                {
                    continue;
                }

                var target = _byAccount.GetOrAdd(id, _ => new ConcurrentDictionary<string, Reading>(StringComparer.Ordinal));
                foreach (var (day, dto) in days)
                {
                    if (!string.IsNullOrWhiteSpace(day) && dto is not null && string.CompareOrdinal(day, cutoff) >= 0)
                    {
                        target[day] = new Reading(dto.Rating, dto.LifetimeTotal, dto.Unanswered, dto.Answered);
                    }
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
                Debug.WriteLine($"Review-history save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-RetentionDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var store = new HistoryFile();

            foreach (var (id, days) in _byAccount)
            {
                var kept = new Dictionary<string, ReadingDto>(StringComparer.Ordinal);
                foreach (var (day, reading) in days)
                {
                    if (string.CompareOrdinal(day, cutoff) >= 0)
                    {
                        kept[day] = new ReadingDto
                        {
                            Rating = reading.Rating,
                            LifetimeTotal = reading.LifetimeTotal,
                            Unanswered = reading.Unanswered,
                            Answered = reading.Answered
                        };
                    }
                    else
                    {
                        days.TryRemove(day, out _);
                    }
                }

                if (kept.Count > 0)
                {
                    store.Accounts[id] = kept;
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

    private readonly record struct Reading(double? Rating, int? LifetimeTotal, int? Unanswered, int? Answered);

    private sealed class HistoryFile
    {
        public Dictionary<string, Dictionary<string, ReadingDto>> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ReadingDto
    {
        public double? Rating { get; set; }
        public int? LifetimeTotal { get; set; }
        public int? Unanswered { get; set; }
        public int? Answered { get; set; }
    }
}

/// <summary>
/// The ambient review-history store the scrape writes to. Override in tests.
/// </summary>
/// <remarks>
/// <b>This exists because the singleton wrote into the owner's real data during a test run.</b>
/// <c>GoogleReviewSnapshotService.ScrapeAsync</c> records a reading, and the scrape tests drive that method
/// with fake instance ids; pointed at <see cref="ReviewHistoryStore.Instance"/> they wrote entries for
/// "g-review-1" into <c>%LOCALAPPDATA%\UnifiedMessenger\review-history.json</c> and overwrote a real day of
/// readings. A test run must never be able to touch live business data, so the write target is now
/// swappable — the same shape as <see cref="InstanceConnection.Current"/>, which exists for the same reason.
/// </remarks>
public static class ReviewHistory
{
    public static ReviewHistoryStore Current { get; set; } = ReviewHistoryStore.Instance;
}
