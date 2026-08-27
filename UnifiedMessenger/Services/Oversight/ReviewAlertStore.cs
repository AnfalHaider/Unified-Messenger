using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Remembers which unhappy reviews have already been notified about.
/// </summary>
/// <remarks>
/// <b>Persistence is what makes the seeding rule mean anything.</b> Held in memory only, every restart would
/// look like a first run — and a first run alerts on nothing, so the owner would simply never be told about
/// a review that arrived while the app was closed. Written to disk, "seen" survives, and the first genuinely
/// new one-star after a restart still interrupts.
/// </remarks>
public sealed class ReviewAlertStore
{
    private const string FileName = "review-alerts.json";

    private static readonly Lazy<ReviewAlertStore> LazyInstance = new(() => new ReviewAlertStore());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ReviewAlertStore Instance => LazyInstance.Value;

    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isLoaded;

    private ReviewAlertStore()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal ReviewAlertStore(string storePath) => _storePath = storePath;

    /// <summary>
    /// False until the first observation has been recorded — the flag that suppresses the install-day burst.
    /// </summary>
    public bool Seeded { get; private set; }

    public ISet<string> Seen() => new HashSet<string>(_seen, StringComparer.Ordinal);

    /// <summary>Records the keys observed in this pass, and marks the installation seeded once one succeeds.</summary>
    /// <param name="passReadSomething">
    /// Whether this pass actually got review data from at least one account.
    /// </param>
    /// <remarks>
    /// <b>Seeding must not complete on a pass that read nothing.</b> An empty key set is ambiguous — it means
    /// either "read fine, no unhappy reviews" or "every scrape failed" — and treating the second as seeded
    /// defeats the whole rule. The failing case is the normal one on a cold start: the first background pass
    /// runs two minutes after launch, the Google sessions are not running scripts yet, and every scrape gives
    /// up. Seeding there would leave the installation marked seeded with zero keys, so the next pass — the
    /// first that actually works — would treat every pre-existing one-star as new and fire the install-day
    /// burst this class exists to prevent.
    /// </remarks>
    public async Task RecordAsync(
        IReadOnlyCollection<string> seen,
        bool passReadSomething,
        CancellationToken cancellationToken = default)
    {
        var changed = false;
        foreach (var key in seen)
        {
            changed |= _seen.Add(key);
        }

        if (!Seeded && passReadSomething)
        {
            Seeded = true;
            changed = true;
        }

        if (changed)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
        }
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

            AlertFile? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer.DeserializeAsync<AlertFile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                CorruptFileRecovery.Preserve(_storePath, "ReviewAlerts", ex);
                return;
            }

            foreach (var key in store?.Seen ?? [])
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _seen.Add(key);
                }
            }

            Seeded = store?.Seeded ?? false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) => SaveAsync(cancellationToken);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = new AlertFile { Seeded = Seeded, Seen = [.. _seen] };

            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
            var tempPath = _storePath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _storePath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("ReviewAlerts", $"Review-alert save failed: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class AlertFile
    {
        public bool Seeded { get; set; }
        public List<string> Seen { get; set; } = [];
    }
}

/// <summary>The ambient alert store. Override in tests so a run cannot touch live data.</summary>
public static class ReviewAlertTracking
{
    public static ReviewAlertStore Current { get; set; } = ReviewAlertStore.Instance;
}
