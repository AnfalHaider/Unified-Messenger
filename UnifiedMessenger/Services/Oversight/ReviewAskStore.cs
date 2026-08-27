using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Remembers which customers have already been asked for a review, so nobody is ever asked twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>This store is the promise.</b> "Ask once, ever" is the rule that keeps this feature from behaving like
/// bulk marketing, and a rule that only holds until the next restart is not a rule. Keyed on phone number
/// because that is the only stable identifier a WhatsApp contact has — display names change, and matching on
/// them would let the same person through under a renamed contact.
/// </para>
/// <para>
/// <b>Nothing is ever removed.</b> There is no expiry and no "ask again after a year": the retention that
/// matters here is remembering, not forgetting. The file holds a phone number and a date per asked customer
/// and nothing else — no names, no message text.
/// </para>
/// </remarks>
public sealed class ReviewAskStore
{
    private const string FileName = "review-asks.json";

    private static readonly Lazy<ReviewAskStore> LazyInstance = new(() => new ReviewAskStore());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ReviewAskStore Instance => LazyInstance.Value;

    // phone -> the local date it was asked on.
    private readonly ConcurrentDictionary<string, string> _asked = new(StringComparer.Ordinal);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isLoaded;

    private ReviewAskStore()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal ReviewAskStore(string storePath) => _storePath = storePath;

    /// <summary>Records that this customer has now been asked. Saved immediately, not debounced.</summary>
    /// <remarks>
    /// Written straight through because the consequence of losing this one write is asking a real person a
    /// second time. That is worth a synchronous file write on a button press.
    /// </remarks>
    public async Task MarkAskedAsync(string phone, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        _asked[phone.Trim()] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Every phone number already asked — the exclusion set for candidate selection.</summary>
    public ISet<string> AskedPhones() => new HashSet<string>(_asked.Keys, StringComparer.Ordinal);

    /// <summary>How many customers were asked in the last <paramref name="days"/> days.</summary>
    /// <remarks>
    /// Reported on its own, never alongside reviews gained. The app can count what it asked and can count
    /// what arrived, but it cannot know that one caused the other, and putting the two numbers side by side
    /// invites exactly that reading.
    /// </remarks>
    public int AskedWithin(int days)
    {
        var cutoff = DateTime.Now.Date.AddDays(-Math.Max(0, days)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return _asked.Values.Count(day => string.CompareOrdinal(day, cutoff) >= 0);
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

            AskFile? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer.DeserializeAsync<AskFile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                CorruptFileRecovery.Preserve(_storePath, "ReviewAsks", ex);
                return;
            }

            foreach (var (phone, day) in store?.Asked ?? [])
            {
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    _asked[phone] = day ?? string.Empty;
                }
            }
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
            var store = new AskFile();
            foreach (var (phone, day) in _asked)
            {
                store.Asked[phone] = day;
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
        catch (Exception ex)
        {
            AppLogger.LogWarning("ReviewAsks", $"Review-ask save failed: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class AskFile
    {
        public Dictionary<string, string> Asked { get; set; } = new(StringComparer.Ordinal);
    }
}

/// <summary>The ambient ask store. Override in tests so a run can never touch live data.</summary>
/// <remarks>
/// Same reason as <see cref="ReviewHistory"/>: a test that records an ask against the singleton would write
/// a real phone number into the owner's file and, worse, permanently exclude that customer from ever being
/// asked.
/// </remarks>
public static class ReviewAsks
{
    public static ReviewAskStore Current { get; set; } = ReviewAskStore.Instance;
}
